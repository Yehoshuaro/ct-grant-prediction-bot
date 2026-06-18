using System.Text;
using GrantAI.Application.Contracts.Responses;

namespace GrantAI.Bot.Formatting;

/// <summary>
/// Turns Application DTOs into human-friendly, Telegram-HTML formatted strings.
/// Kept separate from the routing/transport so the wording is easy to tweak.
/// The metrics mirror the published data: applications, participants, and the
/// share clearing the entrance threshold (КТ порог).
/// </summary>
internal static class MessageFormatter
{
    public static string Welcome() =>
        "<b>👋 Welcome to GrantAI KZ</b>\n\n" +
        "I analyse Kazakhstan master's-degree complex-testing (КТ) statistics and " +
        "estimate the chance of clearing the entrance threshold for a program group.\n\n" +
        "Try <code>/speciality M094</code> or <code>/help</code> to see everything I can do.";

    public static string Help() =>
        "<b>GrantAI KZ — commands</b>\n\n" +
        "<code>/speciality M094</code> — latest summary for a program group\n" +
        "<code>/history M094</code> — full campaign history and trends\n" +
        "<code>/forecast M094</code> — predicted next-campaign pass rate\n" +
        "<code>/chance M094</code> — probability of clearing the threshold\n" +
        "<code>/compare M094</code> — summer vs winter comparison\n\n" +
        "Codes are educational program groups such as <b>M094</b>, <b>M010</b>, <b>M001</b>.";

    public static string Usage(string command, string example) =>
        $"⚠️ Usage: <code>{command} {example}</code>";

    public static string NotFound(string code) =>
        $"🔍 I don't have any imported data for <b>{Display.Escape(code)}</b> yet.";

    public static string Summary(SpecialtySummaryDto s)
    {
        var sb = new StringBuilder();
        sb.Append("<b>📊 ").Append(Display.Escape(s.Code)).Append("</b>");
        if (!string.IsNullOrWhiteSpace(s.Name) &&
            !string.Equals(s.Name, s.Code, StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(" — ").Append(Display.Escape(s.Name));
        }
        sb.Append("\n\n");
        sb.Append("Campaigns on record: <b>").Append(s.CampaignCount).Append("</b>\n");
        sb.Append("Latest campaign: <b>").Append(s.LatestYear).Append(' ')
          .Append(Display.Season(s.LatestSeason)).Append("</b>\n");
        sb.Append("Applications: <b>").Append(s.LatestApplications).Append("</b>, ")
          .Append("participants: <b>").Append(s.LatestParticipants).Append("</b>\n");
        sb.Append("Threshold pass rate: <b>").Append(Display.Percent(s.LatestPassRate)).Append("</b>\n\n");
        sb.Append("➡️ <code>/forecast ").Append(Display.Escape(s.Code)).Append("</code> for a prediction.");
        return sb.ToString();
    }

    public static string History(AdmissionHistoryDto h)
    {
        var sb = new StringBuilder();
        sb.Append("<b>📜 History — ").Append(Display.Escape(h.Code)).Append("</b>\n");
        if (!string.IsNullOrWhiteSpace(h.GroupName) &&
            !string.Equals(h.GroupName, h.Code, StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("<i>").Append(Display.Escape(h.GroupName)).Append("</i>\n");
        }
        sb.Append('\n');

        foreach (var p in h.Points)
        {
            sb.Append("<b>").Append(Display.Escape(p.Label)).Append("</b>\n");
            sb.Append("   applications ").Append(p.Applications)
              .Append(", participants ").Append(p.Participants)
              .Append(" (").Append(Display.Percent(p.ParticipationRate)).Append(")\n");
            sb.Append("   passed <b>").Append(p.PassedThreshold).Append("</b>")
              .Append(", pass rate <b>").Append(Display.Percent(p.PassRate)).Append("</b>\n");
        }

        sb.Append("\n<b>Trends</b>\n");
        sb.Append("Applications: ").Append(Display.Trend(h.ApplicationsTrend)).Append('\n');
        sb.Append("Participants: ").Append(Display.Trend(h.ParticipantsTrend)).Append('\n');
        sb.Append("Pass rate: ").Append(Display.Trend(h.PassRateTrend));
        return sb.ToString();
    }

    public static string Forecast(ForecastDto f)
    {
        var sb = new StringBuilder();
        sb.Append("<b>🔮 Forecast — ").Append(Display.Escape(f.Code)).Append("</b>\n\n");
        sb.Append("Predicted pass rate: <b>").Append(Display.Percent(f.PredictedPassRate)).Append("</b>\n");
        sb.Append("Likely range: <b>").Append(Display.Percent(f.LowerBound)).Append('–')
          .Append(Display.Percent(f.UpperBound)).Append("</b>\n");
        sb.Append("Confidence: <b>").Append(f.ConfidencePercent).Append("%</b>\n");
        sb.Append("Trend: ").Append(Display.Trend(f.Trend)).Append('\n');
        sb.Append("Based on <b>").Append(f.DataPoints).Append("</b> campaigns (")
          .Append(Display.Escape(f.Method)).Append(")\n");

        if (f.Factors.Count > 0)
        {
            sb.Append("\n<b>Main factors</b>\n");
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
        sb.Append("<b>🎯 Chance — ").Append(Display.Escape(p.Code)).Append("</b>\n\n");
        sb.Append("Probability of clearing the threshold: <b>")
          .Append(p.PassProbabilityPercent).Append("%</b>\n");
        sb.Append("Likely range: <b>").Append(p.LowerBoundPercent).Append('–')
          .Append(p.UpperBoundPercent).Append("%</b>\n");
        sb.Append("Based on a forecasted pass rate of <b>")
          .Append(Display.Percent(p.PredictedPassRate)).Append("</b>")
          .Append(" (confidence ").Append(p.ConfidencePercent).Append("%)\n");

        if (p.Factors.Count > 0)
        {
            sb.Append("\n<b>Factors</b>\n");
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
        sb.Append("<b>⚖️ Comparison — ").Append(Display.Escape(c.Code)).Append("</b>\n\n");

        if (c.BySeason.Count > 0)
        {
            sb.Append("<b>By season</b>\n");
            foreach (var s in c.BySeason)
            {
                sb.Append("• <b>").Append(Display.Season(s.Season)).Append("</b>: pass rate ")
                  .Append(Display.Percent(s.AveragePassRate)).Append(", turn-out ")
                  .Append(Display.Percent(s.AverageParticipationRate)).Append(", avg applications ")
                  .Append(Display.Num(s.AverageApplications)).Append(" (").Append(s.CampaignCount).Append(")\n");
            }
        }

        if (!string.IsNullOrWhiteSpace(c.Summary))
        {
            sb.Append('\n').Append("<i>").Append(Display.Escape(c.Summary)).Append("</i>");
        }
        return sb.ToString();
    }

    public static string Error() =>
        "😕 Something went wrong while handling that. Please try again in a moment.";
}
