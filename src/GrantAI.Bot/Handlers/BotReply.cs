using Telegram.Bot.Types.ReplyMarkups;

namespace GrantAI.Bot.Handlers;

/// <summary>
/// A bot reply: text plus an optional inline keyboard. Returned by
/// <see cref="CommandRouter"/> so the transport layer (BotHostedService) is
/// the only place that talks to the Telegram client.
/// </summary>
public sealed record BotReply(string Text, InlineKeyboardMarkup? Keyboard = null)
{
    public static BotReply Plain(string text) => new(text, null);
}
