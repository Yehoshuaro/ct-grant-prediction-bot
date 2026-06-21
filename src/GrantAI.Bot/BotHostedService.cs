using GrantAI.Bot.Formatting;
using GrantAI.Bot.Handlers;
using GrantAI.Bot.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GrantAI.Bot;

/// <summary>
/// Long-polls Telegram for the lifetime of the host. Each incoming message or
/// callback query is routed through <see cref="CommandRouter"/>; the reply
/// carries an optional inline keyboard which is sent back together with the text.
/// </summary>
public sealed class BotHostedService : BackgroundService, IUpdateHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly CommandRouter _router;
    private readonly BotRateLimiter _rateLimiter;
    private readonly ILogger<BotHostedService> _logger;

    public BotHostedService(
        ITelegramBotClient bot,
        CommandRouter router,
        BotRateLimiter rateLimiter,
        ILogger<BotHostedService> logger)
    {
        _bot = bot;
        _router = router;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var me = await _bot.GetMe(stoppingToken).ConfigureAwait(false);
            _logger.LogInformation("Telegram bot @{Username} (id {Id}) started; long-polling for updates",
                me.Username, me.Id);

            await RegisterCommandsAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not reach Telegram on startup; check TELEGRAM_BOT_TOKEN");
        }

        // Pragmatic liveness: the bot has no HTTP surface, so we emit a periodic
        // heartbeat. Docker restart-on-failure handles the recovery path.
        _ = Task.Run(() => HeartbeatLoopAsync(stoppingToken), stoppingToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
            DropPendingUpdates = true
        };

        await _bot.ReceiveAsync(this, receiverOptions, stoppingToken).ConfigureAwait(false);
    }

    private async Task HeartbeatLoopAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(5);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
                _logger.LogInformation("Bot heartbeat ok");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.CallbackQuery is { } callback)
        {
            await HandleCallbackAsync(botClient, callback, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (update.Message is not { Text: { Length: > 0 } text } message)
        {
            return;
        }

        if (!_rateLimiter.TryAcquire(message.Chat.Id))
        {
            await SendReplyAsync(botClient, message.Chat.Id,
                BotReply.Plain(MessageFormatter.TooManyRequests()), cancellationToken).ConfigureAwait(false);
            return;
        }

        var reply = await _router.RouteAsync(text, cancellationToken).ConfigureAwait(false);
        await SendReplyAsync(botClient, message.Chat.Id, reply, cancellationToken).ConfigureAwait(false);
    }

    public Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        HandleErrorSource source,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram polling error from {Source}", source);
        return Task.CompletedTask;
    }

    private async Task HandleCallbackAsync(
        ITelegramBotClient botClient, CallbackQuery callback, CancellationToken ct)
    {
        var chatId = callback.Message?.Chat.Id ?? callback.From.Id;

        // Always acknowledge first so the user's button stops spinning.
        try
        {
            await botClient.AnswerCallbackQuery(callback.Id, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to acknowledge callback {CallbackId}", callback.Id);
        }

        if (!_rateLimiter.TryAcquire(chatId))
        {
            await SendReplyAsync(botClient, chatId,
                BotReply.Plain(MessageFormatter.TooManyRequests()), ct).ConfigureAwait(false);
            return;
        }

        var command = CallbackData.ToCommand(callback.Data);
        if (command is null)
        {
            await SendReplyAsync(botClient, chatId, BotReply.Plain(MessageFormatter.UnknownCallback()), ct).ConfigureAwait(false);
            return;
        }

        var reply = await _router.RouteAsync(command, ct).ConfigureAwait(false);
        await SendReplyAsync(botClient, chatId, reply, ct).ConfigureAwait(false);
    }

    private async Task SendReplyAsync(
        ITelegramBotClient botClient, long chatId, BotReply reply, CancellationToken ct)
    {
        try
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: reply.Text,
                parseMode: ParseMode.Html,
                replyMarkup: reply.Keyboard,
                cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reply to chat {ChatId}", chatId);

            try
            {
                await botClient.SendMessage(chatId, MessageFormatter.Error(),
                    cancellationToken: ct).ConfigureAwait(false);
            }
            catch
            {
                // Nothing more we can do for this update.
            }
        }
    }

    private async Task RegisterCommandsAsync(CancellationToken ct)
    {
        var commands = new[]
        {
            new BotCommand { Command = "start", Description = "Запуск и краткое описание" },
            new BotCommand { Command = "help", Description = "Список команд" },
            new BotCommand { Command = "speciality", Description = "Сводка по ГОП (например M094)" },
            new BotCommand { Command = "history", Description = "История кампаний по ГОП" },
            new BotCommand { Command = "forecast", Description = "Прогноз доли прошедших порог" },
            new BotCommand { Command = "chance", Description = "Шанс пройти порог в ГОП" },
            new BotCommand { Command = "compare", Description = "Сравнение лета и зимы" },
            new BotCommand { Command = "grant", Description = "Прогноз проходного балла на грант" }
        };

        try
        {
            await _bot.SetMyCommands(commands, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Non-fatal: the bot still works without the menu UI hint.
            _logger.LogWarning(ex, "Could not register bot command list with Telegram");
        }
    }
}
