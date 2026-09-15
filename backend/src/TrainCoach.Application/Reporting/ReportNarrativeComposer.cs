using System.Text;
using TrainCoach.Domain.Enums;

namespace TrainCoach.Application.Reporting;

/// <summary>
/// Turns the snapshot + triggered rules into a short plain-language paragraph. Purely
/// template-based composition of the rule engine's own output — it does not add any judgment
/// of its own, so the text always stays traceable back to a specific rule.
/// </summary>
public class ReportNarrativeComposer : IReportNarrativeComposer
{
    public string Compose(ReportInputSnapshot s, IReadOnlyList<GeneratedReportInsightDto> insights)
    {
        var sb = new StringBuilder();

        sb.Append(s.Type == ReportType.Morning
            ? $"Dobré ráno, {s.AthleteFirstName}! "
            : $"Dobrý večer, {s.AthleteFirstName}! ");

        if (s.Type == ReportType.Morning)
        {
            sb.Append(s.IsRestDay
                ? "Dnes máš v plánu odpočinek. "
                : $"Dnes tě čeká: {s.TodayWorkoutTitle}. ");
        }
        else
        {
            sb.Append(s.CompletedPlannedWorkout == true
                ? "Podle večerního check-inu se ti dnešní trénink podařilo splnit. "
                : s.IsRestDay
                    ? "Dnes byl na plánu odpočinek. "
                    : string.Empty);
        }

        if (insights.Count == 0)
        {
            sb.Append("Podle dostupných dat nevidíme nic, co by vybočovalo z tvého obvyklého vzorce.");
        }
        else
        {
            sb.Append("Na co se dnes zaměřit: ");
            sb.Append(string.Join(' ', insights.Select(i => i.Message)));
        }

        sb.Append(" Tento report je automaticky generovaný na základě jednoduchých pravidel a tvého osobního průměru — není to lékařské doporučení ani diagnóza.");

        return sb.ToString();
    }
}
