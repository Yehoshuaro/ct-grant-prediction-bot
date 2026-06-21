using System.Text;
using GrantAI.Application.Contracts.Responses;

namespace GrantAI.Bot.Formatting;

internal static class MessageFormatter
{
    public static string Welcome() =>
        "<b>GrantAI KZ</b>\n\n" +
        "Бот по статистике комплексного тестирования (КТ) в магистратуру " +
        "Казахстана. Считает, насколько труден порог в группе образовательной " +
        "программы (ГОП) и прогнозирует проходной балл на грант.\n\n" +
        "Пример: <code>/speciality M094</code>. Полный список команд: /help.";

    public static string Help() =>
        "<b>Команды</b>\n\n" +
        "<code>/speciality M094</code> сводка по ГОП за последнюю кампанию\n" +
        "<code>/history M094</code> вся история кампаний и тренды\n" +
        "<code>/forecast M094</code> прогноз доли участников, набравших порог\n" +
        "<code>/chance M094</code> шанс пройти порог в этой группе\n" +
        "<code>/compare M094</code> сравнение лета и зимы\n" +
        "<code>/grant M094</code> прогноз проходного балла на грант\n\n" +
        "Коды: <b>M094</b>, <b>M010</b>, <b>M001</b> и другие коды ГОП.";

    public static string Usage(string command, string example) =>
        $"Использование: <code>{command} {example}</code>";

    public static string NotFound(string code) =>
        $"Нет данных для <b>{Display.Escape(code)}</b>. Импортируйте кампании в API или попробуйте другой код.";

    public static string TooManyRequests() =>
        "Слишком много запросов. Попробуйте через несколько секунд.";

    public static string UnknownCallback() =>
        "Действие недоступно. Используйте /help для списка команд.";

    public static string Summary(SpecialtySummaryDto s)
    {
        var sb = new StringBuilder();
        sb.Append("<b>").Append(Display.Escape(s.Code)).Append("</b>");
        if (!string.IsNullOrWhiteSpace(s.Name) &&
            !string.Equals(s.Name, s.Code, StringComparison.OrdinalIgnoreCase))
        {
            sb.Append('\n').Append("<i>").Append(Display.Escape(s.Name)).Append("</i>");
        }
        sb.Append("\n\n");
        sb.Append("Кампаний в базе: <b>").Append(s.CampaignCount).Append("</b>\n");
        sb.Append("Последняя кампания: <b>").Append(s.LatestYear).Append(' ')
          .Append(Display.Season(s.LatestSeason)).Append("</b>\n");
        sb.Append("Заявок: <b>").Append(s.LatestApplications).Append("</b>, ")
          .Append("участников: <b>").Append(s.LatestParticipants).Append("</b>\n");
        sb.Append("Доля прошедших порог: <b>").Append(Display.Percent(s.LatestPassRate)).Append("</b>\n\n");
        sb.Append("Прогноз: <code>/forecast ").Append(Display.Escape(s.Code)).Append("</code>");
        return sb.ToString();
    }

    public static string History(AdmissionHistoryDto h)
    {
        var sb = new StringBuilder();
        sb.Append("<b>История ").Append(Display.Escape(h.Code)).Append("</b>\n");
        if (!string.IsNullOrWhiteSpace(h.GroupName) &&
            !string.Equals(h.GroupName, h.Code, StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("<i>").Append(Display.Escape(h.GroupName)).Append("</i>\n");
        }
        sb.Append('\n');

        foreach (var p in h.Points)
        {
            sb.Append("<b>").Append(Display.Escape(p.Label)).Append("</b>\n");
            sb.Append("   заявок ").Append(p.Applications)
              .Append(", участников ").Append(p.Participants)
              .Append(" (").Append(Display.Percent(p.ParticipationRate)).Append(")\n");
            sb.Append("   прошли порог <b>").Append(p.PassedThreshold).Append("</b>")
              .Append(", доля <b>").Append(Display.Percent(p.PassRate)).Append("</b>\n");
        }

        sb.Append("\n<b>Тренды</b>\n");
        sb.Append("Заявки: ").Append(Display.Trend(h.ApplicationsTrend)).Append('\n');
        sb.Append("Участники: ").Append(Display.Trend(h.ParticipantsTrend)).Append('\n');
        sb.Append("Доля прохождения порога: ").Append(Display.Trend(h.PassRateTrend));
        return sb.ToString();
    }

    public static string Forecast(ForecastDto f)
    {
        var sb = new StringBuilder();
        sb.Append("<b>Прогноз ").Append(Display.Escape(f.Code)).Append("</b>\n\n");
        sb.Append("Прогноз доли прошедших порог: <b>")
          .Append(Display.Percent(f.PredictedPassRate)).Append("</b>\n");
        sb.Append("Диапазон: <b>от ").Append(Display.Percent(f.LowerBound))
          .Append(" до ").Append(Display.Percent(f.UpperBound)).Append("</b>\n");
        sb.Append("Уверенность: <b>").Append(f.ConfidencePercent).Append("%</b>\n");
        sb.Append("Тренд: ").Append(Display.Trend(f.Trend)).Append('\n');
        sb.Append("Кампаний в расчёте: <b>").Append(f.DataPoints).Append("</b> (")
          .Append(Display.Escape(f.Method)).Append(")\n");

        if (f.Factors.Count > 0)
        {
            sb.Append("\n<b>Факторы</b>\n");
            foreach (var factor in f.Factors)
            {
                sb.Append("• ").Append(Display.Escape(factor)).Append('\n');
            }
        }

        if (!string.IsNullOrWhiteSpace(f.Explanation))
        {
            sb.Append('\n').Append("<i>").Append(Display.Escape(f.Explanation)).Append("</i>");
        }
        return sb.ToString();
    }

    public static string Probability(ProbabilityDto p)
    {
        var sb = new StringBuilder();
        sb.Append("<b>Шанс пройти порог ").Append(Display.Escape(p.Code)).Append("</b>\n\n");
        sb.Append("Это шанс набрать минимальный порог КТ в этой группе, а не шанс получить грант.\n\n");
        sb.Append("Оценка вероятности: <b>").Append(p.PassProbabilityPercent).Append("%</b>\n");
        sb.Append("Диапазон: <b>от ").Append(p.LowerBoundPercent)
          .Append(" до ").Append(p.UpperBoundPercent).Append("%</b>\n");
        sb.Append("Опирается на прогноз доли прошедших порог <b>")
          .Append(Display.Percent(p.PredictedPassRate)).Append("</b>")
          .Append(" (уверенность ").Append(p.ConfidencePercent).Append("%)\n");

        if (p.Factors.Count > 0)
        {
            sb.Append("\n<b>Факторы</b>\n");
            foreach (var factor in p.Factors)
            {
                sb.Append("• ").Append(Display.Escape(factor)).Append('\n');
            }
        }

        if (!string.IsNullOrWhiteSpace(p.Explanation))
        {
            sb.Append('\n').Append("<i>").Append(Display.Escape(p.Explanation)).Append("</i>");
        }
        return sb.ToString();
    }

    public static string Comparison(ComparisonDto c)
    {
        var sb = new StringBuilder();
        sb.Append("<b>Сравнение сезонов ").Append(Display.Escape(c.Code)).Append("</b>\n\n");

        if (c.BySeason.Count > 0)
        {
            sb.Append("<b>По сезонам</b>\n");
            foreach (var s in c.BySeason)
            {
                sb.Append("• <b>").Append(Display.Season(s.Season)).Append("</b>: ")
                  .Append("порог ").Append(Display.Percent(s.AveragePassRate))
                  .Append(", явка ").Append(Display.Percent(s.AverageParticipationRate))
                  .Append(", в среднем ").Append(Display.Num(s.AverageApplications)).Append(" заявок (")
                  .Append(s.CampaignCount).Append(" кампаний)\n");
            }
        }

        if (!string.IsNullOrWhiteSpace(c.Summary))
        {
            sb.Append('\n').Append("<i>").Append(Display.Escape(c.Summary)).Append("</i>");
        }
        return sb.ToString();
    }

    public static string GrantForecast(string code, IReadOnlyList<GrantForecastDto> forecasts)
    {
        var sb = new StringBuilder();
        sb.Append("<b>Прогноз проходного балла на грант ").Append(Display.Escape(code.ToUpperInvariant())).Append("</b>\n");

        // Название ГОП одинаково для обоих треков; берём его у первой записи с непустым именем.
        var name = forecasts.Select(f => f.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        if (!string.IsNullOrWhiteSpace(name))
        {
            sb.Append("<i>").Append(Display.Escape(name)).Append("</i>\n");
        }
        sb.Append('\n');

        for (var i = 0; i < forecasts.Count; i++)
        {
            var f = forecasts[i];
            if (i > 0) sb.Append('\n');

            sb.Append("<b>").Append(Display.MasterTrack(f.MasterType)).Append("</b> ")
              .Append("(шкала 0-").Append(f.ScoreScaleMax).Append(")\n");
            sb.Append("Для гранта в следующем наборе вероятно понадобится около ")
              .Append("<b>").Append(f.PredictedCutoff).Append(" из ").Append(f.ScoreScaleMax).Append("</b>\n");
            sb.Append("Диапазон: <b>от ").Append(f.LowerBound).Append(" до ").Append(f.UpperBound)
              .Append(" из ").Append(f.ScoreScaleMax).Append("</b>\n");
            sb.Append("Уверенность: <b>").Append(f.ConfidencePercent).Append("%</b>, ")
              .Append("тренд: ").Append(Display.Trend(f.Trend)).Append('\n');
            sb.Append("Лет данных в расчёте: <b>").Append(f.DataPoints).Append("</b>\n");

            if (f.Factors.Count > 0)
            {
                foreach (var factor in f.Factors)
                {
                    sb.Append("• ").Append(Display.Escape(factor)).Append('\n');
                }
            }

            if (!string.IsNullOrWhiteSpace(f.Explanation))
            {
                sb.Append("<i>").Append(Display.Escape(f.Explanation)).Append("</i>\n");
            }
        }

        sb.Append("\n<i>Обычно доступно только 2-3 года данных, поэтому значение носит ориентировочный характер.</i>");
        return sb.ToString();
    }

    public static string Error() =>
        "Не удалось обработать запрос. Попробуйте ещё раз через минуту.";
}
