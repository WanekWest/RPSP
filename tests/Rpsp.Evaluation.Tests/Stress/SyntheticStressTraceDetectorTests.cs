using Rpsp.Core.Observations;
using Rpsp.Evaluation.Stress;
using Xunit;

namespace Rpsp.Evaluation.Tests.Stress;

public sealed class SyntheticStressTraceDetectorTests
{
    [Fact]
    public async Task StableUrbanBaselineKeepsDetectorScoresLow()
    {
        StressAssessment assessment = await AssessFullTraceAsync(SyntheticStressTraceKind.LongStableUrbanBaseline);

        Assert.True(assessment.Stationary.RiskScore.Value <= 0.2D);
        Assert.True(assessment.Rat.RiskScore.Value <= 0.2D);
        Assert.True(assessment.Unknown.RiskScore.Value <= 0.2D);
        Assert.True(assessment.Neighbor.RiskScore.Value <= 0.2D);
        Assert.True(assessment.Composite.RiskScore.Value <= 0.25D);
    }

    [Fact]
    public async Task BenignUrbanMobilityDoesNotCreateHighStationaryOrNeighborRisk()
    {
        StressAssessment assessment = await AssessFullTraceAsync(SyntheticStressTraceKind.LongBenignUrbanMobility);

        Assert.True(assessment.Stationary.RiskScore.Value <= 0.3D);
        Assert.True(assessment.Neighbor.RiskScore.Value <= 0.3D);
        Assert.True(assessment.Composite.RiskScore.Value < 0.8D);
        Assert.True(assessment.Stationary.IsSuppressed);
        Assert.True(assessment.Neighbor.IsSuppressed);
    }

    [Fact]
    public async Task IndoorFadingStressDoesNotCreateHighStationaryRisk()
    {
        StressAssessment assessment = await AssessFullTraceAsync(SyntheticStressTraceKind.IndoorFadingStress);

        Assert.True(assessment.Stationary.RiskScore.Value <= 0.3D);
        Assert.True(assessment.Composite.RiskScore.Value < 0.8D);
    }

    [Fact]
    public async Task BorderOscillationStressDoesNotCreateHighCompositeRisk()
    {
        StressAssessment assessment = await AssessFullTraceAsync(SyntheticStressTraceKind.BorderOscillationStress);

        Assert.True(assessment.Composite.RiskScore.Value < 0.8D);
    }

    [Fact]
    public async Task RepeatedUnknownStrongCellStressCreatesHighUnknownAndCompositeRisk()
    {
        StressAssessment assessment = await AssessFullTraceAsync(SyntheticStressTraceKind.RepeatedUnknownStrongCellStress);

        Assert.True(assessment.Unknown.RiskScore.Value >= 0.8D);
        Assert.True(assessment.Composite.RiskScore.Value >= 0.8D);
    }

    [Fact]
    public async Task RepeatedNeighborGraphDriftStressCreatesHighNeighborAndCompositeRisk()
    {
        StressAssessment assessment = await AssessFullTraceAsync(SyntheticStressTraceKind.RepeatedNeighborGraphDriftStress);

        Assert.True(assessment.Neighbor.RiskScore.Value >= 0.8D);
        Assert.True(assessment.Composite.RiskScore.Value >= 0.8D);
    }

    [Fact]
    public async Task UnavailableMetricsStressDoesNotCreateHighRiskFromMissingData()
    {
        StressAssessment assessment = await AssessFullTraceAsync(SyntheticStressTraceKind.NoisyUnavailableMetricsStress);

        Assert.True(assessment.Stationary.RiskScore.Value <= 0.3D);
        Assert.True(assessment.Rat.RiskScore.Value <= 0.3D);
        Assert.True(assessment.Unknown.RiskScore.Value <= 0.3D);
        Assert.True(assessment.Neighbor.RiskScore.Value <= 0.3D);
        Assert.True(assessment.Composite.RiskScore.Value <= 0.35D);
    }

    [Fact]
    public async Task ColdStartStressSuppressesEarlyWindows()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(SyntheticStressTraceKind.ColdStartThenWarmupStress);
        IReadOnlyList<RadioObservation> observations = await StressTraceTestSupport
            .CollectAsync(trace);
        SyntheticStressWindow earlyWindow = StressTraceTestSupport.RequireWindow(trace, "early-cold-start");
        StressAssessment early = StressTraceTestSupport.Assess(
            StressTraceTestSupport.Window(observations, earlyWindow),
            trace);
        SyntheticStressWindow warmWindow = StressTraceTestSupport.RequireWindow(trace, "warm-baseline");
        StressAssessment warm = StressTraceTestSupport.Assess(
            StressTraceTestSupport.Window(observations, warmWindow),
            trace);

        Assert.True(early.Stationary.IsSuppressed);
        Assert.True(early.Unknown.IsSuppressed);
        Assert.True(early.Neighbor.IsSuppressed);
        Assert.True(warm.Composite.RiskScore.Value <= 0.25D);
    }

    [Fact]
    public async Task RatNoServiceLegacyDwellStressCreatesHighRatRiskOnlyInFocusedWindow()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(SyntheticStressTraceKind.RatNoServiceLegacyDwellStress);
        IReadOnlyList<RadioObservation> observations = await StressTraceTestSupport
            .CollectAsync(trace);
        StressAssessment preOutage = StressTraceTestSupport.Assess(
            StressTraceTestSupport.Window(observations, StressTraceTestSupport.RequireWindow(trace, "pre-outage")),
            trace);
        StressAssessment legacyDwell = StressTraceTestSupport.Assess(
            StressTraceTestSupport.Window(observations, StressTraceTestSupport.RequireWindow(trace, "legacy-dwell")),
            trace);
        StressAssessment postRecovery = StressTraceTestSupport.Assess(
            StressTraceTestSupport.Window(observations, StressTraceTestSupport.RequireWindow(trace, "post-recovery")),
            trace);

        Assert.True(preOutage.Rat.RiskScore.Value <= 0.2D);
        Assert.True(legacyDwell.Rat.RiskScore.Value >= 0.8D);
        Assert.True(legacyDwell.Composite.RiskScore.Value >= 0.8D);
        Assert.True(postRecovery.Rat.RiskScore.Value <= 0.2D);
    }

    [Fact]
    public async Task MaintenanceOutageRecoveryStressDoesNotCreateRatDowngradeRisk()
    {
        StressAssessment assessment = await AssessFullTraceAsync(SyntheticStressTraceKind.MaintenanceOutageRecoveryStress);

        Assert.True(assessment.Rat.RiskScore.Value <= 0.2D);
        Assert.True(assessment.Rat.IsSuppressed);
        Assert.True(assessment.Composite.RiskScore.Value <= 0.35D);
    }

    [Fact]
    public async Task MotionUnknownMixedChurnStressSuppressesDetectorFamilies()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(
            SyntheticStressTraceKind.MotionUnknownMixedChurnSuppressionStress);
        IReadOnlyList<RadioObservation> observations = await StressTraceTestSupport
            .CollectAsync(trace);
        SyntheticStressWindow churnWindow = StressTraceTestSupport.RequireWindow(trace, "unknown-motion-churn");
        StressAssessment assessment = StressTraceTestSupport.Assess(
            StressTraceTestSupport.Window(observations, churnWindow),
            trace);

        Assert.True(assessment.Stationary.IsSuppressed);
        Assert.True(assessment.Rat.IsSuppressed);
        Assert.True(assessment.Unknown.IsSuppressed);
        Assert.True(assessment.Neighbor.IsSuppressed);
        Assert.True(assessment.Composite.IsSuppressed);
        Assert.True(assessment.Composite.RiskScore.Value <= 0.2D);
    }

    [Fact]
    public async Task UnknownStrongNeighborOnlyLongStressDoesNotCreateHighCompositeRisk()
    {
        StressAssessment assessment = await AssessFullTraceAsync(SyntheticStressTraceKind.UnknownStrongNeighborOnlyLongStress);

        Assert.True(assessment.Unknown.RiskScore.Value > 0D);
        Assert.True(assessment.Unknown.RiskScore.Value <= 0.56D);
        Assert.True(assessment.Neighbor.RiskScore.Value <= 0.3D);
        Assert.True(assessment.Composite.RiskScore.Value < 0.6D);
    }

    [Fact]
    public async Task BaselineAgingDriftStressRemainsBelowHighRisk()
    {
        StressAssessment assessment = await AssessFullTraceAsync(SyntheticStressTraceKind.BaselineAgingDriftStress);

        Assert.True(assessment.Unknown.RiskScore.Value <= 0.2D);
        Assert.True(assessment.Composite.RiskScore.Value < 0.8D);
    }

    [Fact]
    public async Task RoamingLikeContextStressIsSuppressedAsAmbiguousLocalEvidence()
    {
        StressAssessment assessment = await AssessFullTraceAsync(SyntheticStressTraceKind.RoamingLikeContextStress);

        Assert.True(assessment.Stationary.IsSuppressed);
        Assert.True(assessment.Unknown.IsSuppressed);
        Assert.True(assessment.Neighbor.IsSuppressed);
        Assert.True(assessment.Composite.RiskScore.Value <= 0.35D);
    }

    [Fact]
    public async Task TemporaryOperatorCellDeploymentStressDoesNotReachHighCompositeRisk()
    {
        StressAssessment assessment = await AssessFullTraceAsync(
            SyntheticStressTraceKind.TemporaryOperatorCellDeploymentStress);

        Assert.True(assessment.Unknown.RiskScore.Value <= 0.6D);
        Assert.True(assessment.Composite.RiskScore.Value < 0.8D);
    }

    [Fact]
    public async Task MetricUnavailableWithChurnStressKeepsCompositeClampedBelowHighRisk()
    {
        StressAssessment assessment = await AssessFullTraceAsync(
            SyntheticStressTraceKind.MetricUnavailableWithChurnStress);

        Assert.True(assessment.Unknown.RiskScore.Value <= 0.2D);
        Assert.InRange(assessment.Composite.RiskScore.Value, 0D, 0.79D);
    }

    [Fact]
    public async Task NearThresholdBorderOscillationStressDoesNotBecomeHighRisk()
    {
        StressAssessment assessment = await AssessFullTraceAsync(
            SyntheticStressTraceKind.NearThresholdBorderOscillationStress);

        Assert.True(assessment.Stationary.RiskScore.Value < 0.8D);
        Assert.True(assessment.Neighbor.RiskScore.Value <= 0.2D);
        Assert.True(assessment.Composite.RiskScore.Value < 0.8D);
    }

    [Fact]
    public async Task DetectorExecutionCompletesAcrossAllBoundedStressTraces()
    {
        foreach (SyntheticStressTrace trace in SyntheticStressTraceCatalog.All)
        {
            IReadOnlyList<RadioObservation> observations = await StressTraceTestSupport
                .CollectAsync(trace);
            StressAssessment assessment = StressTraceTestSupport.Assess(observations, trace);

            Assert.InRange(observations.Count, 1, 48);
            Assert.InRange(assessment.Composite.RiskScore.Value, 0D, 1D);
        }
    }

    private static async Task<StressAssessment> AssessFullTraceAsync(SyntheticStressTraceKind kind)
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(kind);
        IReadOnlyList<RadioObservation> observations = await StressTraceTestSupport
            .CollectAsync(trace)
            .ConfigureAwait(false);

        return StressTraceTestSupport.Assess(observations, trace);
    }
}
