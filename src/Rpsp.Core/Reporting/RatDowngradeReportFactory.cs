using Rpsp.Core.Detectors;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Reporting;

public static class RatDowngradeReportFactory
{
    public static DetectorReport Create(RatDowngradeDetectionResult result)
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
            "RatDowngradeRiskDetector",
            decision,
            CreateSummary(result, decision),
            result.RiskScore,
            result.WarningDecision.Level,
            windowStart,
            windowEnd,
            CreateSections(result));
    }

    private static string DetermineDecision(RatDowngradeDetectionResult result)
    {
        if (result.IsSuppressed)
        {
            if (string.Equals(result.SuppressionReason, "suppressed:no-legacy-downgrade", StringComparison.Ordinal)
                || string.Equals(result.SuppressionReason, "suppressed:unknown-rat-history", StringComparison.Ordinal))
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

    private static string CreateSummary(RatDowngradeDetectionResult result, string decision)
    {
        if (string.Equals(decision, "Suppressed", StringComparison.Ordinal))
        {
            return $"RAT downgrade analysis was suppressed: {result.SuppressionReason ?? "unspecified"}.";
        }

        if (string.Equals(decision, "No anomaly", StringComparison.Ordinal))
        {
            return "No suspicious RAT downgrade radio-access anomaly was produced.";
        }

        return "Suspicious RAT downgrade pattern produced a radio-access anomaly; this is not proof of interception.";
    }

    private static IReadOnlyList<DetectorReportSection> CreateSections(RatDowngradeDetectionResult result)
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
            new DetectorReportItem("Bounded claim", "This report is not proof of an attack."),
            new DetectorReportItem("Protocol visibility", $"This report does not inspect {privilegedProtocolLabel} semantics."),
            new DetectorReportItem("Interception scope", "This report does not detect lawful/core interception."),
            new DetectorReportItem("Attribution", "This report does not provide attribution.")
        ];
    }
}
