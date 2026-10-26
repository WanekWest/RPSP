using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;
using Xunit;

namespace Rpsp.Core.Tests.Scoring;

public sealed class CompositeRiskScorerTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AllLowOrSuppressedContributionsProduceLowCompositeScore()
    {
        var scorer = new CompositeRiskScorer();

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            CreateContribution("StationaryHandoverAnomalyDetector", 0D, RiskSeverity.None, true, "suppressed:no-serving-cell-churn"),
            CreateContribution("RatDowngradeRiskDetector", 0D, RiskSeverity.None, true, "suppressed:no-legacy-downgrade")
        ]);

        Assert.True(assessment.RiskScore.Value <= 0.1D);
        Assert.Equal(RiskSeverity.None, assessment.RiskScore.Severity);
        Assert.Equal(WarningLevel.None, assessment.WarningDecision.Level);
        Assert.True(assessment.IsSuppressed);
    }

    [Fact]
    public void SingleSuspiciousStationaryHandoverDrivesSuspiciousCompositeScore()
    {
        var scorer = new CompositeRiskScorer();

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            CreateContribution("StationaryHandoverAnomalyDetector", 0.65D, RiskSeverity.Medium),
            CreateContribution("RatDowngradeRiskDetector", 0D, RiskSeverity.None, true, "suppressed:no-legacy-downgrade")
        ]);

        Assert.InRange(assessment.RiskScore.Value, 0.6D, 0.79D);
        Assert.Equal(RiskSeverity.Medium, assessment.RiskScore.Severity);
        Assert.Equal(WarningLevel.Caution, assessment.WarningDecision.Level);
        Assert.Equal("StationaryHandoverAnomalyDetector", assessment.HighestContributingDetector);
    }

    [Fact]
    public void SingleSuspiciousRatDowngradeDrivesSuspiciousCompositeScore()
    {
        var scorer = new CompositeRiskScorer();

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            CreateContribution("StationaryHandoverAnomalyDetector", 0D, RiskSeverity.None, true, "suppressed:no-serving-cell-churn"),
            CreateContribution("RatDowngradeRiskDetector", 0.65D, RiskSeverity.Medium)
        ]);

        Assert.InRange(assessment.RiskScore.Value, 0.6D, 0.79D);
        Assert.Equal(RiskSeverity.Medium, assessment.RiskScore.Severity);
        Assert.Equal("RatDowngradeRiskDetector", assessment.HighestContributingDetector);
    }

    [Fact]
    public void SingleHighRiskContributionProducesHighCompositeScore()
    {
        var scorer = new CompositeRiskScorer();

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            CreateContribution("StationaryHandoverAnomalyDetector", 0.85D, RiskSeverity.High),
            CreateContribution("RatDowngradeRiskDetector", 0D, RiskSeverity.None, true, "suppressed:no-legacy-downgrade")
        ]);

        Assert.True(assessment.RiskScore.Value >= 0.8D);
        Assert.Equal(RiskSeverity.High, assessment.RiskScore.Severity);
        Assert.Equal(WarningLevel.Warning, assessment.WarningDecision.Level);
    }

    [Fact]
    public void MultipleSuspiciousContributionsIncreaseScoreWithBoundedBoost()
    {
        var scorer = new CompositeRiskScorer();

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            CreateContribution("StationaryHandoverAnomalyDetector", 0.65D, RiskSeverity.Medium),
            CreateContribution("RatDowngradeRiskDetector", 0.62D, RiskSeverity.Medium)
        ]);

        Assert.True(assessment.RiskScore.Value > 0.65D);
        Assert.True(assessment.CorrelationBoost <= 0.25D);
        Assert.Equal(0.10D, assessment.CorrelationBoost, precision: 6);
        Assert.InRange(assessment.RiskScore.Value, 0.75D, 0.76D);
    }

    [Fact]
    public void SuppressedMovingContributionsDoNotIncreaseCompositeRisk()
    {
        var scorer = new CompositeRiskScorer();

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            CreateContribution("StationaryHandoverAnomalyDetector", 0D, RiskSeverity.None, true, "suppressed:benign-motion-state"),
            CreateContribution("RatDowngradeRiskDetector", 0D, RiskSeverity.None, true, "suppressed:benign-motion-state")
        ]);

        Assert.Equal(0D, assessment.RiskScore.Value);
        Assert.Equal(0D, assessment.CorrelationBoost);
        Assert.Equal(2, assessment.SuppressedContributionCount);
    }

    [Fact]
    public void CompositeScoreIsClampedToValidRange()
    {
        var scorer = new CompositeRiskScorer(
            new CompositeRiskScorerOptions
            {
                AdditionalSuspiciousBoost = 1D,
                AdditionalHighRiskBoost = 1D,
                MaximumCorrelationBoost = 1D
            });

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            CreateContribution("DetectorA", 0.95D, RiskSeverity.High),
            CreateContribution("DetectorB", 0.95D, RiskSeverity.High),
            CreateContribution("DetectorC", 0.95D, RiskSeverity.High)
        ]);

        Assert.InRange(assessment.RiskScore.Value, 0D, 1D);
        Assert.Equal(1D, assessment.RiskScore.Value);
    }

    [Fact]
    public void HighestContributingDetectorIsPreserved()
    {
        var scorer = new CompositeRiskScorer();

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            CreateContribution("StationaryHandoverAnomalyDetector", 0.62D, RiskSeverity.Medium),
            CreateContribution("RatDowngradeRiskDetector", 0.82D, RiskSeverity.High)
        ]);

        Assert.Equal("RatDowngradeRiskDetector", assessment.HighestContributingDetector);
        Assert.Equal(0.82D, assessment.BaseScore);
    }

    [Fact]
    public void CompositeEvidenceIncludesContributionCountAndBoostEvidence()
    {
        var scorer = new CompositeRiskScorer();

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            CreateContribution("StationaryHandoverAnomalyDetector", 0.65D, RiskSeverity.Medium),
            CreateContribution("RatDowngradeRiskDetector", 0.62D, RiskSeverity.Medium)
        ]);

        AssertEvidenceContains(assessment, "TotalDetectorCount", "2");
        AssertEvidenceContains(assessment, "ActiveContributionCount", "2");
        AssertEvidenceContains(assessment, "CorrelationBoost", "0.100");
        AssertEvidenceContains(assessment, "FinalScore", "0.750");
    }

    private static void AssertEvidenceContains(
        CompositeRiskAssessment assessment,
        string evidenceName,
        string expectedDescriptionFragment)
    {
        EvidenceItem evidenceItem = Assert.Single(
            assessment.Evidence.Items,
            item => string.Equals(item.Name, evidenceName, StringComparison.Ordinal));

        Assert.Contains(expectedDescriptionFragment, evidenceItem.Description, StringComparison.Ordinal);
    }

    private static DetectorRiskContribution CreateContribution(
        string detectorName,
        double score,
        RiskSeverity severity,
        bool isSuppressed = false,
        string? suppressionReason = null)
    {
        var riskScore = new RiskScore(score, severity, confidence: 0.8D);
        var evidence = new EvidenceBundle(
            string.Create(null, $"evidence:{detectorName}:{score:F3}:{isSuppressed}"),
            [new EvidenceItem("SyntheticEvidence", "Synthetic detector evidence for composite scoring tests.", ObservationAvailability.Derived, score)],
            [CreateObservation(detectorName)]);

        return new DetectorRiskContribution(
            detectorName,
            riskScore,
            ToWarningLevel(severity),
            evidence,
            isSuppressed,
            suppressionReason);
    }

    private static RadioObservation CreateObservation(string detectorName)
    {
        return new RadioObservation(
            string.Create(null, $"observation:{detectorName}"),
            Start,
            ObservationSourceKind.Replay,
            []);
    }

    private static WarningLevel ToWarningLevel(RiskSeverity severity)
    {
        return severity switch
        {
            RiskSeverity.High => WarningLevel.Warning,
            RiskSeverity.Critical => WarningLevel.Critical,
            RiskSeverity.Medium => WarningLevel.Caution,
            RiskSeverity.Low => WarningLevel.Informational,
            _ => WarningLevel.None
        };
    }
}
