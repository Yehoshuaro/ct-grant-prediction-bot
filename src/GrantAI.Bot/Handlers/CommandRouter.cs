using GrantAI.Application.Specialties;
using GrantAI.Bot.Formatting;
using Microsoft.Extensions.Logging;

namespace GrantAI.Bot.Handlers;

/// <summary>
/// Pure command interpreter: maps a raw message string to a formatted reply by
/// calling the Application read facade. It has no Telegram dependency, so the
/// transport (polling, sending) stays in <c>BotHostedService</c> and the routing
/// logic here is straightforward to follow and test.
/// </summary>
public sealed class CommandRouter
{
    private readonly ISpecialtyQueryService _specialties;
    private readonly ILogger<CommandRouter> _logger;

    public CommandRouter(ISpecialtyQueryService specialties, ILogger<CommandRouter> logger)
    {
        _specialties = specialties;
        _logger = logger;
    }

    public async Task<string> RouteAsync(string? text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return MessageFormatter.Help();
        }

        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = NormaliseCommand(parts[0]);
        var args = parts.Skip(1).ToArray();

        _logger.LogInformation("Bot command {Command} with {ArgCount} arg(s)", command, args.Length);

        try
        {
            return command switch
            {
                "start" => MessageFormatter.Welcome(),
                "help" => MessageFormatter.Help(),
                "speciality" or "specialty" => await SpecialityAsync(args, ct),
                "history" => await HistoryAsync(args, ct),
                "forecast" => await ForecastAsync(args, ct),
                "chance" => await ChanceAsync(args, ct),
                "compare" => await CompareAsync(args, ct),
                _ => MessageFormatter.Help()
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle command {Command}", command);
            return MessageFormatter.Error();
        }
    }

    private async Task<string> SpecialityAsync(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
        {
            return MessageFormatter.Usage("/speciality", "M094");
        }

        var code = args[0];
        var summary = await _specialties.GetSpecialtyAsync(code, ct);
        return summary is null ? MessageFormatter.NotFound(code) : MessageFormatter.Summary(summary);
    }

    private async Task<string> HistoryAsync(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
        {
            return MessageFormatter.Usage("/history", "M094");
        }

        var code = args[0];
        var history = await _specialties.GetHistoryAsync(code, ct);
        return history.Points.Count == 0 ? MessageFormatter.NotFound(code) : MessageFormatter.History(history);
    }

    private async Task<string> ForecastAsync(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
        {
            return MessageFormatter.Usage("/forecast", "M094");
        }

        var code = args[0];
        var forecast = await _specialties.GetForecastAsync(code, ct);
        return forecast.DataPoints == 0 ? MessageFormatter.NotFound(code) : MessageFormatter.Forecast(forecast);
    }

    private async Task<string> ChanceAsync(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
        {
            return MessageFormatter.Usage("/chance", "M094");
        }

        var code = args[0];
        var probability = await _specialties.GetChanceAsync(code, ct);
        return probability.DataPoints == 0
            ? MessageFormatter.NotFound(code)
            : MessageFormatter.Probability(probability);
    }

    private async Task<string> CompareAsync(string[] args, CancellationToken ct)
    {
        if (args.Length < 1)
        {
            return MessageFormatter.Usage("/compare", "M094");
        }

        var code = args[0];
        var comparison = await _specialties.GetComparisonAsync(code, ct);
        return comparison.BySeason.Count == 0
            ? MessageFormatter.NotFound(code)
            : MessageFormatter.Comparison(comparison);
    }

    /// <summary>Strips the leading slash and any <c>@BotName</c> suffix, lower-cased.</summary>
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
