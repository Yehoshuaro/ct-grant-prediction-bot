using GrantAI.Bot.Handlers;
using Telegram.Bot.Types.ReplyMarkups;

namespace GrantAI.Bot.Formatting;

/// <summary>
/// Builds inline keyboards. Buttons are encoded with <see cref="CallbackData"/>;
/// any payload that would overflow Telegram's 64-byte limit is silently dropped
/// (the user simply doesn't see that button rather than being offered a broken one).
/// </summary>
internal static class Keyboards
{
    /// <summary>Welcome / help screen: pick a quick action with a small example code.</summary>
    public static InlineKeyboardMarkup MainMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                Button("Сводка по M094", "speciality", "M094"),
                Button("Прогноз M094", "forecast", "M094")
            },
            new[]
            {
                Button("Список команд", "help")
            }
        });
    }

    /// <summary>Actions available for a specific ГОП code, attached to its summary.</summary>
    public static InlineKeyboardMarkup ForCode(string code)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                Button("Прогноз", "forecast", code),
                Button("История", "history", code)
            },
            new[]
            {
                Button("Шанс", "chance", code),
                Button("Сравнение", "compare", code)
            },
            new[]
            {
                Button("Грант", "grant", code)
            }
        });
    }

    private static InlineKeyboardButton Button(string label, string action, string? argument = null)
    {
        var data = CallbackData.Build(action, argument) ?? action;
        return InlineKeyboardButton.WithCallbackData(label, data);
    }
}
