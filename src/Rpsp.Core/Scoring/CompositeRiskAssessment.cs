using Rpsp.Core.Events;

namespace Rpsp.Core.Scoring;

public sealed record CompositeRiskAssessment
{
    public CompositeRiskAssessment(
        EvidenceBundle evidence,
        RiskScore riskScore,
        WarningDecision warningDecision,
        IReadOnlyList<DetectorRiskContribution> contributions,
        int totalDetectorCount,
        int activeContributionCount,
        int suppressedContributionCount,
        string? highestContributingDetector,
        double baseScore,
        double correlationBoost)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(riskScore);
        ArgumentNullException.ThrowIfNull(warningDecision);
        ArgumentNullException.ThrowIfNull(contributions);

        Evidence = evidence;
        RiskScore = riskScore;
        WarningDecision = warningDecision;
        Contributions = contributions;
        TotalDetectorCount = totalDetectorCount;
        ActiveContributionCount = activeContributionCount;
        SuppressedContributionCount = suppressedContributionCount;
        HighestContributingDetector = highestContributingDetector;
        BaseScore = ValidateUnitInterval(baseScore, nameof(baseScore));
        CorrelationBoost = ValidateUnitInterval(correlationBoost, nameof(correlationBoost));
    }

    public EvidenceBundle Evidence { get; }

    public RiskScore RiskScore { get; }

    public WarningDecision WarningDecision { get; }

    public IReadOnlyList<DetectorRiskContribution> Contributions { get; }

    public int TotalDetectorCount { get; }

    public int ActiveContributionCount { get; }

    public int SuppressedContributionCount { get; }

    public string? HighestContributingDetector { get; }

    public double BaseScore { get; }

    public double CorrelationBoost { get; }

    public bool IsSuppressed => TotalDetectorCount > 0 && SuppressedContributionCount == TotalDetectorCount;

    private static double ValidateUnitInterval(double value, string parameterName)
    {
        if (double.IsNaN(value) || value < 0D || value > 1D)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }
}
