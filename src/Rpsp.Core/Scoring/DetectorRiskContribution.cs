using Rpsp.Core.Detectors;
using Rpsp.Core.Events;

namespace Rpsp.Core.Scoring;

public sealed record DetectorRiskContribution
{
    public DetectorRiskContribution(
        string detectorName,
        RiskScore riskScore,
        WarningLevel warningLevel,
        EvidenceBundle evidence,
        bool isSuppressed,
        string? suppressionReason = null,
        AnomalyEvent? eventRecord = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detectorName);
        ArgumentNullException.ThrowIfNull(riskScore);
        ArgumentNullException.ThrowIfNull(evidence);

        DetectorName = detectorName;
        RiskScore = riskScore;
        WarningLevel = warningLevel;
        Evidence = evidence;
        IsSuppressed = isSuppressed;
        SuppressionReason = suppressionReason;
        Event = eventRecord;
    }

    public string DetectorName { get; }

    public RiskScore RiskScore { get; }

    public WarningLevel WarningLevel { get; }

    public EvidenceBundle Evidence { get; }

    public bool IsSuppressed { get; }

    public string? SuppressionReason { get; }

    public AnomalyEvent? Event { get; }

    public static DetectorRiskContribution FromStationaryHandover(
        StationaryHandoverDetectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new DetectorRiskContribution(
            "StationaryHandoverAnomalyDetector",
            result.RiskScore,
            result.WarningDecision.Level,
            result.Evidence,
            result.IsSuppressed,
            result.SuppressionReason,
            result.Event);
    }

    public static DetectorRiskContribution FromRatDowngrade(
        RatDowngradeDetectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new DetectorRiskContribution(
            "RatDowngradeRiskDetector",
            result.RiskScore,
            result.WarningDecision.Level,
            result.Evidence,
            result.IsSuppressed,
            result.SuppressionReason,
            result.Event);
    }

    public static DetectorRiskContribution FromUnknownStrongCell(
        UnknownStrongCellDetectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new DetectorRiskContribution(
            "UnknownStrongCellDetector",
            result.RiskScore,
            result.WarningDecision.Level,
            result.Evidence,
            result.IsSuppressed,
            result.SuppressionReason,
            result.Event);
    }

    public static DetectorRiskContribution FromNeighborGraph(
        NeighborGraphDetectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new DetectorRiskContribution(
            "NeighborGraphAnomalyDetector",
            result.RiskScore,
            result.WarningDecision.Level,
            result.Evidence,
            result.IsSuppressed,
            result.SuppressionReason,
            result.Event);
    }
}
