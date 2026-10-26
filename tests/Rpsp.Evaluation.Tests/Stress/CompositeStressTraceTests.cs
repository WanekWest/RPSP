using Rpsp.Core.Observations;
using Rpsp.Evaluation.Stress;
using Xunit;

namespace Rpsp.Evaluation.Tests.Stress;

public sealed class CompositeStressTraceTests
{
    [Fact]
    public async Task MixedSuspiciousBurstCreatesHighCompositeRiskInBurstWindow()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(SyntheticStressTraceKind.MixedSuspiciousBurst);
        IReadOnlyList<RadioObservation> observations = await StressTraceTestSupport
            .CollectAsync(trace);
        StressAssessment preBurst = StressTraceTestSupport.Assess(
            StressTraceTestSupport.Window(observations, StressTraceTestSupport.RequireWindow(trace, "pre-burst")),
            trace);
        StressAssessment burst = StressTraceTestSupport.Assess(
            StressTraceTestSupport.Window(observations, StressTraceTestSupport.RequireWindow(trace, "burst")),
            trace);
        StressAssessment postBurst = StressTraceTestSupport.Assess(
            StressTraceTestSupport.Window(observations, StressTraceTestSupport.RequireWindow(trace, "post-burst")),
            trace);

        Assert.True(preBurst.Composite.RiskScore.Value <= 0.25D);
        Assert.True(burst.Composite.RiskScore.Value >= 0.8D);
        Assert.True(postBurst.Composite.RiskScore.Value <= 0.25D);
        Assert.True(burst.Composite.RiskScore.Value > burst.HighestIndividualScore);
        Assert.InRange(burst.Composite.RiskScore.Value, 0D, 1D);
    }

    [Fact]
    public async Task CompositeMultiDetectorCorrelationStressIsBoostedAndClamped()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(
            SyntheticStressTraceKind.CompositeMultiDetectorCorrelationStress);
        IReadOnlyList<RadioObservation> observations = await StressTraceTestSupport
            .CollectAsync(trace);
        SyntheticStressWindow correlatedWindow = StressTraceTestSupport.RequireWindow(trace, "correlated-burst");
        StressAssessment focusedAssessment = StressTraceTestSupport.Assess(
            StressTraceTestSupport.Window(observations, correlatedWindow),
            trace);
        bool anyBoostedWindow = false;

        foreach (IReadOnlyList<RadioObservation> window in StressTraceTestSupport.SlidingWindows(observations, 8))
        {
            StressAssessment assessment = StressTraceTestSupport.Assess(window, trace);

            Assert.InRange(assessment.Composite.RiskScore.Value, 0D, 1D);

            if (assessment.Composite.RiskScore.Value > assessment.HighestIndividualScore)
            {
                anyBoostedWindow = true;
            }
        }

        Assert.True(focusedAssessment.Composite.RiskScore.Value >= 0.8D);
        Assert.True(focusedAssessment.Composite.RiskScore.Value <= 1D);
        Assert.True(anyBoostedWindow);
    }

    [Fact]
    public async Task CompositeLowScoreAccumulationStressDoesNotBoostBelowHighRisk()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(
            SyntheticStressTraceKind.CompositeLowScoreAccumulationStress);
        IReadOnlyList<RadioObservation> observations = await StressTraceTestSupport
            .CollectAsync(trace);
        SyntheticStressWindow lowScoreWindow = StressTraceTestSupport.RequireWindow(trace, "low-score-mix");
        StressAssessment assessment = StressTraceTestSupport.Assess(
            StressTraceTestSupport.Window(observations, lowScoreWindow),
            trace);

        Assert.InRange(assessment.Stationary.RiskScore.Value, 0.6D, 0.79D);
        Assert.True(assessment.Unknown.RiskScore.Value <= 0.56D);
        Assert.Equal(0D, assessment.Composite.CorrelationBoost);
        Assert.True(assessment.Composite.RiskScore.Value < 0.8D);
    }
}
