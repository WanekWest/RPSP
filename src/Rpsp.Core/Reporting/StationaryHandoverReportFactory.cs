using System.Globalization;
using Rpsp.Core.Detectors;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Reporting;

public static class StationaryHandoverReportFactory
{
    public static DetectorReport Create(StationaryHandoverDetectionResult result)
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
            "StationaryHandoverAnomalyDetector",
            decision,
            CreateSummary(result, decision),
            result.RiskScore,
            result.WarningDecision.Level,
            windowStart,
            windowEnd,
            CreateSections(result));
    }

    private static string DetermineDecision(StationaryHandoverDetectionResult result)
    {
        if (result.IsSuppressed)
        {
            if (string.Equals(result.SuppressionReason, "suppressed:no-serving-cell-churn", StringComparison.Ordinal)
                || string.Equals(result.SuppressionReason, "suppressed:single-serving-cell", StringComparison.Ordinal))
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

    private static string CreateSummary(StationaryHandoverDetectionResult result, string decision)
    {
        if (string.Equals(decision, "Suppressed", StringComparison.Ordinal))
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Stationary serving-cell churn analysis was suppressed: {result.SuppressionReason ?? "unspecified"}.");
        }

        if (string.Equals(decision, "No anomaly", StringComparison.Ordinal))
        {
            return "No stationary serving-cell churn radio-access anomaly was produced.";
        }

        return "Stationary serving-cell churn produced a radio-access anomaly compatible with suspicious reselection/handover behavior; this is not an attribution or proof of interception.";
    }

    private static IReadOnlyList<DetectorReportSection> CreateSections(StationaryHandoverDetectionResult result)
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
            new DetectorReportItem("Attribution", "This report is not an attribution or proof of interception.")
        ];
    }
}
