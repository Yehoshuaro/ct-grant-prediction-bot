using GrantAI.Application.Common.Results;
using GrantAI.Application.Contracts.Responses;
using GrantAI.Application.Specialties;
using GrantAI.Bot.Formatting;
using Microsoft.Extensions.Logging;

namespace GrantAI.Bot.Handlers;

/// <summary>
/// Pure command interpreter: turns a raw message into a <see cref="BotReply"/>
/// by calling the Application read facades. Result/Error from the Application
/// layer is mapped to either a formatted message + keyboard, or to a Russian
/// "not found" reply with no keyboard.
/// </summary>
public sealed class CommandRouter
{
    private readonly ISpecialtyQueryService _specialties;
    private readonly IGrantQueryService _grants;
    private readonly ILogger<CommandRouter> _logger;

    public CommandRouter(
        ISpecialtyQueryService specialties,
        IGrantQueryService grants,
        ILogger<CommandRouter> logger)
    {
        _specialties = specialties;
        _grants = grants;
        _logger = logger;
    }

    public async Task<BotReply> RouteAsync(string? text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new BotReply(MessageFormatter.Help(), Keyboards.MainMenu());
        }

        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = NormaliseCommand(parts[0]);
        var args = parts.Skip(1).ToArray();

        _logger.LogInformation("Bot command {Command} with {ArgCount} arg(s)", command, args.Length);

        try
        {
            return command switch
            {
                "start" => new BotReply(MessageFormatter.Welcome(), Keyboards.MainMenu()),
                "help" => new BotReply(MessageFormatter.Help(), Keyboards.MainMenu()),
                "speciality" or "specialty" => await SpecialityAsync(args, ct).ConfigureAwait(false),
                "history" => await HistoryAsync(args, ct).ConfigureAwait(false),
                "forecast" => await ForecastAsync(args, ct).ConfigureAwait(false),
                "chance" => await ChanceAsync(args, ct).ConfigureAwait(false),
                "compare" => await CompareAsync(args, ct).ConfigureAwait(false),
                "grant" => await GrantAsync(args, ct).ConfigureAwait(false),
                _ => new BotReply(MessageFormatter.Help(), Keyboards.MainMenu())
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle command {Command}", command);
            return BotReply.Plain(MessageFormatter.Error());
        }
    }

    private async Task<BotReply> SpecialityAsync(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
            return BotReply.Plain(MessageFormatter.Usage("/speciality", "M094"));

        var code = args[0];
        var result = await _specialties.GetSpecialtyAsync(code, ct).ConfigureAwait(false);
        return ToReply(result, code, summary => new BotReply(MessageFormatter.Summary(summary), Keyboards.ForCode(summary.Code)));
    }

    private async Task<BotReply> HistoryAsync(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
            return BotReply.Plain(MessageFormatter.Usage("/history", "M094"));

        var code = args[0];
        var result = await _specialties.GetHistoryAsync(code, ct).ConfigureAwait(false);
        return ToReply(result, code, history => new BotReply(MessageFormatter.History(history), Keyboards.ForCode(history.Code)));
    }

    private async Task<BotReply> ForecastAsync(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
            return BotReply.Plain(MessageFormatter.Usage("/forecast", "M094"));

        var code = args[0];
        var result = await _specialties.GetForecastAsync(code, ct).ConfigureAwait(false);
        return ToReply(result, code, forecast => new BotReply(MessageFormatter.Forecast(forecast), Keyboards.ForCode(forecast.Code)));
    }

    private async Task<BotReply> ChanceAsync(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
            return BotReply.Plain(MessageFormatter.Usage("/chance", "M094"));

        var code = args[0];
        var result = await _specialties.GetChanceAsync(code, ct).ConfigureAwait(false);
        return ToReply(result, code, probability => new BotReply(MessageFormatter.Probability(probability), Keyboards.ForCode(probability.Code)));
    }

    private async Task<BotReply> CompareAsync(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
            return BotReply.Plain(MessageFormatter.Usage("/compare", "M094"));

        var code = args[0];
        var result = await _specialties.GetComparisonAsync(code, ct).ConfigureAwait(false);
        return ToReply(result, code, comparison => new BotReply(MessageFormatter.Comparison(comparison), Keyboards.ForCode(comparison.Code)));
    }

    private async Task<BotReply> GrantAsync(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
            return BotReply.Plain(MessageFormatter.Usage("/grant", "M094"));

        var code = args[0];
        var result = await _grants.GetForecastAsync(code, ct).ConfigureAwait(false);
        return ToReply(result, code, forecasts =>
            new BotReply(MessageFormatter.GrantForecast(code, forecasts), Keyboards.ForCode(code.ToUpperInvariant())));
    }

    private static BotReply ToReply<T>(Result<T> result, string code, Func<T, BotReply> onSuccess)
    {
        return result.TryGetValue(out var value, out _)
            ? onSuccess(value)
            : BotReply.Plain(MessageFormatter.NotFound(code));
    }

    private static string NormaliseCommand(string token)
    {
        var command = token.StartsWith('/') ? token[1..] : token;
        var at = command.IndexOf('@');
        if (at >= 0)
        {
            command = command[..at];
        }
        return command.ToLowerInvariant();
    }
}
