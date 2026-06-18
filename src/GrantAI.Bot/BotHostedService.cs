using GrantAI.Bot.Formatting;
using GrantAI.Bot.Handlers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GrantAI.Bot;

/// <summary>
/// Long-polls Telegram for the lifetime of the host. Each incoming text message
/// is handed to <see cref="CommandRouter"/> and the formatted reply is sent back
/// with HTML parse mode. Implementing <see cref="IUpdateHandler"/> keeps the
/// receive loop and error handling in one focused place.
/// </summary>
public sealed class BotHostedService : BackgroundService, IUpdateHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly CommandRouter _router;
    private readonly ILogger<BotHostedService> _logger;

    public BotHostedService(ITelegramBotClient bot, CommandRouter router, ILogger<BotHostedService> logger)
    {
        _bot = bot;
        _router = router;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var me = await _bot.GetMe(stoppingToken);
            _logger.LogInformation("Telegram bot @{Username} (id {Id}) started; long-polling for updates",
                me.Username, me.Id);
        }
        catch (Exception ex)
        {
            // A bad/missing token surfaces here. Log clearly and let the loop
            // below retry through the standard polling error path.
            _logger.LogError(ex, "Could not reach Telegram on startup; check TELEGRAM_BOT_TOKEN");
        }

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message],
            DropPendingUpdates = true
        };

        await _bot.ReceiveAsync(this, receiverOptions, stoppingToken);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: { Length: > 0 } text } message)
        {
            return;
        }

        var reply = await _router.RouteAsync(text, cancellationToken);

        try
        {
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: reply,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reply to chat {ChatId}", message.Chat.Id);

            // Retry once without formatting in case the HTML was the problem.
            try
            {
                await botClient.SendMessage(message.Chat.Id, MessageFormatter.Error(),
                    cancellationToken: cancellationToken);
            }
            catch
            {
                // Nothing more we can do for this update.
            }
        }
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
}
