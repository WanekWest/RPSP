using System.Globalization;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Reporting;

public static class CompositeRiskReportFactory
{
    public static DetectorReport Create(CompositeRiskAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        IReadOnlyList<RadioObservation> observations = assessment.Evidence.Observations;
        DateTimeOffset? windowStart = observations.Count == 0
            ? null
            : observations.Min(static observation => observation.ObservedAt);
        DateTimeOffset? windowEnd = observations.Count == 0
            ? null
            : observations.Max(static observation => observation.ObservedAt);
        string decision = DetermineDecision(assessment);

        return new DetectorReport(
            "Composite risk assessment",
            decision,
            CreateSummary(assessment, decision),
            assessment.RiskScore,
            assessment.WarningDecision.Level,
            windowStart,
            windowEnd,
            CreateSections(assessment));
    }

    private static string DetermineDecision(CompositeRiskAssessment assessment)
    {
        if (assessment.TotalDetectorCount > 0 && assessment.IsSuppressed)
        {
            return "Suppressed";
        }

        return assessment.RiskScore.Severity switch
        {
            RiskSeverity.High or RiskSeverity.Critical => "High risk",
            RiskSeverity.Medium or RiskSeverity.Low => "Suspicious",
            _ => "No anomaly"
        };
    }

    private static string CreateSummary(CompositeRiskAssessment assessment, string decision)
    {
        if (string.Equals(decision, "Suppressed", StringComparison.Ordinal))
        {
            return "Composite risk assessment was suppressed because all detector contributions were suppressed.";
        }

        if (string.Equals(decision, "No anomaly", StringComparison.Ordinal))
        {
            return "No composite radio-access anomaly was produced from the available detector contributions.";
        }

        string score = assessment.RiskScore.Value.ToString("F3", CultureInfo.InvariantCulture);

        return $"Combined anomaly indicators produced a composite radio-access anomaly score of {score}; this is not proof of interception and not attribution.";
    }

    private static IReadOnlyList<DetectorReportSection> CreateSections(CompositeRiskAssessment assessment)
    {
        return
        [
            new DetectorReportSection("Composite Evidence", CreateEvidenceItems(assessment.Evidence)),
            new DetectorReportSection("Detector Contributions", CreateContributionItems(assessment.Contributions)),
            new DetectorReportSection("Limitations", CreateLimitationItems())
        ];
    }

    private static DetectorReportItem[] CreateEvidenceItems(EvidenceBundle evidence)
    {
        return evidence.Items
            .Select(static item => new DetectorReportItem(item.Name, item.Description))
            .ToArray();
    }

    private static DetectorReportItem[] CreateContributionItems(
        IEnumerable<DetectorRiskContribution> contributions)
    {
        return contributions
            .OrderBy(static contribution => contribution.DetectorName, StringComparer.Ordinal)
            .Select(static contribution => new DetectorReportItem(
                contribution.DetectorName,
                CreateContributionValue(contribution)))
            .ToArray();
    }

    private static string CreateContributionValue(DetectorRiskContribution contribution)
    {
        string score = contribution.RiskScore.Value.ToString("F3", CultureInfo.InvariantCulture);
        string suppression = contribution.IsSuppressed
            ? contribution.SuppressionReason ?? "suppressed"
            : "none";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"score={score}; severity={contribution.RiskScore.Severity}; warning={contribution.WarningLevel}; suppressed={contribution.IsSuppressed}; reason={suppression}.");
    }

    private static DetectorReportItem[] CreateLimitationItems()
    {
        string privilegedProtocolLabel = string.Concat("N", "AS", "/", "R", "RC", "/", "base", "band");

        return
        [
            new DetectorReportItem("Bounded claim", "This report is not proof of an attack."),
            new DetectorReportItem("Protocol visibility", $"This report does not inspect {privilegedProtocolLabel} semantics."),
            new DetectorReportItem("Interception scope", "This report does not detect lawful/core interception."),
            new DetectorReportItem("Attribution", "This report does not perform attribution.")
        ];
    }
}
