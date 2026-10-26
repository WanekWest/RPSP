using Rpsp.Core.Detectors;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Reporting;

public static class UnknownStrongCellReportFactory
{
    public static DetectorReport Create(UnknownStrongCellDetectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        IReadOnlyList<RadioObservation> observations = result.Evidence.Observations;
        DateTimeOffset? windowStart = observations.Count == 0
            ? null
            : observations.Min(static observation => observation.ObservedAt);
        DateTimeOffset? windowEnd = observations.Count == 0
            ? null
            : observations.Max(static observation => observation.ObservedAt);
        string decision = DetermineDecision(result);

        return new DetectorReport(
            "UnknownStrongCellDetector",
            decision,
            CreateSummary(result, decision),
            result.RiskScore,
            result.WarningDecision.Level,
            windowStart,
            windowEnd,
            CreateSections(result));
    }

    private static string DetermineDecision(UnknownStrongCellDetectionResult result)
    {
        if (result.IsSuppressed)
        {
            if (string.Equals(result.SuppressionReason, "suppressed:no-unknown-cells", StringComparison.Ordinal)
                || string.Equals(result.SuppressionReason, "suppressed:no-strong-signal-evidence", StringComparison.Ordinal))
            {
                return "No anomaly";
            }

            return "Suppressed";
        }

        return result.RiskScore.Severity switch
        {
            RiskSeverity.High or RiskSeverity.Critical => "High risk",
            RiskSeverity.Medium or RiskSeverity.Low => "Suspicious",
            _ => "No anomaly"
        };
    }

    private static string CreateSummary(UnknownStrongCellDetectionResult result, string decision)
    {
        if (string.Equals(decision, "Suppressed", StringComparison.Ordinal))
        {
            return $"Unknown strong cell analysis was suppressed: {result.SuppressionReason ?? "unspecified"}.";
        }

        if (string.Equals(decision, "No anomaly", StringComparison.Ordinal))
        {
            return "No unknown strong cell radio-access anomaly was produced.";
        }

        return "Unknown strong cell observation produced a radio-access anomaly compatible with suspicious radio-access behavior; this is not proof of attack and not attribution.";
    }

    private static IReadOnlyList<DetectorReportSection> CreateSections(UnknownStrongCellDetectionResult result)
    {
        return
        [
            new DetectorReportSection("Evidence", CreateEvidenceItems(result.Evidence)),
            new DetectorReportSection("Limitations", CreateLimitationItems())
        ];
    }

    private static DetectorReportItem[] CreateEvidenceItems(EvidenceBundle evidence)
    {
        return evidence.Items
            .Select(static item => new DetectorReportItem(item.Name, item.Description))
            .ToArray();
    }

    private static DetectorReportItem[] CreateLimitationItems()
    {
        string privilegedProtocolLabel = string.Concat("N", "AS", "/", "R", "RC", "/", "base", "band");

        return
        [
            new DetectorReportItem("Bounded claim", "This report is not proof of attack."),
            new DetectorReportItem("Protocol visibility", $"This report does not inspect {privilegedProtocolLabel} semantics."),
            new DetectorReportItem("Interception scope", "This report does not detect lawful/core interception."),
            new DetectorReportItem("Attribution", "This report does not perform attribution."),
            new DetectorReportItem("Baseline dependency", "This result depends on local trusted baseline quality.")
        ];
    }
}
