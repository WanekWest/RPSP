using Rpsp.Core.Pipeline;
using Rpsp.Core.Reporting;
using Rpsp.Core.Scoring;
using Rpsp.Evaluation.Scenarios;
using Rpsp.Evaluation.Stress;
using Xunit;

namespace Rpsp.Evaluation.Tests;

public sealed class ReplayPipelineRunnerTests
{
    [Fact]
    public void StableReplayTraceProducesLowRiskFullTraceWindow()
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(ReplayScenarioKind.StationaryStableServingCell);

        ReplayPipelineRunResult result = ReplayPipelineRunner.Run(
            scenario.Trace,
            new RpspDetectionPipeline(),
            windowStrategy: ReplayPipelineWindowStrategy.FullTrace);
        ReplayPipelineWindowResult window = Assert.Single(result.Windows);

        Assert.Equal("full-trace", window.WindowName);
        Assert.Equal(scenario.Trace.Frames.Count, window.ObservationCount);
        Assert.True(window.CompositeAssessment.RiskScore.Value < 0.25D);
    }

    [Fact]
    public void MixedSuspiciousBurstIsHighRiskOnlyInBurstWindow()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(SyntheticStressTraceKind.MixedSuspiciousBurst);

        ReplayPipelineRunResult result = ReplayPipelineRunner.Run(trace);
        ReplayPipelineWindowResult preBurst = FindWindow(result, "pre-burst");
        ReplayPipelineWindowResult burst = FindWindow(result, "burst");
        ReplayPipelineWindowResult postBurst = FindWindow(result, "post-burst");

        Assert.True(preBurst.CompositeAssessment.RiskScore.Value < 0.8D);
        Assert.True(burst.CompositeAssessment.RiskScore.Value >= 0.8D);
        Assert.True(postBurst.CompositeAssessment.RiskScore.Value < 0.8D);
    }

    [Fact]
    public void StressTraceDefaultUsesDeclaredWindows()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(SyntheticStressTraceKind.MixedSuspiciousBurst);

        ReplayPipelineRunResult result = ReplayPipelineRunner.Run(trace);

        Assert.Equal(trace.Windows.Count, result.Windows.Count);
        Assert.Equal(trace.Windows.Select(static window => window.Name), result.Windows.Select(static window => window.WindowName));
    }

    [Fact]
    public void ReportsAreDeterministicAcrossIdenticalRuns()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(SyntheticStressTraceKind.MixedSuspiciousBurst);
        var formatter = new PlainTextDetectorReportFormatter();

        ReplayPipelineRunResult first = ReplayPipelineRunner.Run(trace);
        ReplayPipelineRunResult second = ReplayPipelineRunner.Run(trace);

        Assert.Equal(
            first.Reports.Select(report => formatter.Format(report)).ToArray(),
            second.Reports.Select(report => formatter.Format(report)).ToArray());
    }

    [Fact]
    public void SlidingWindowResultsKeepAllScoresClamped()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(
            SyntheticStressTraceKind.CompositeMultiDetectorCorrelationStress);

        ReplayPipelineRunResult result = ReplayPipelineRunner.Run(
            trace,
            windowStrategy: ReplayPipelineWindowStrategy.Sliding(windowSize: 6, step: 3));

        Assert.NotEmpty(result.Windows);

        foreach (ReplayPipelineWindowResult window in result.Windows)
        {
            Assert.InRange(window.CompositeAssessment.RiskScore.Value, 0D, 1D);

            foreach (DetectorRiskContribution contribution in window.PipelineResult.DetectorContributions)
            {
                Assert.InRange(contribution.RiskScore.Value, 0D, 1D);
            }
        }
    }

    [Fact]
    public void FullTraceStressRunnerExposesCompositeAssessmentsAndReports()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(SyntheticStressTraceKind.LongStableUrbanBaseline);

        ReplayPipelineRunResult result = ReplayPipelineRunner.Run(
            trace,
            windowStrategy: ReplayPipelineWindowStrategy.FullTrace);

        Assert.Single(result.Windows);
        Assert.Single(result.CompositeAssessments);
        Assert.Single(result.Reports);
        Assert.True(result.CompositeAssessments[0].RiskScore.Value <= 0.25D);
    }

    private static ReplayPipelineWindowResult FindWindow(
        ReplayPipelineRunResult result,
        string windowName)
    {
        return result.Windows.Single(window =>
            string.Equals(window.WindowName, windowName, StringComparison.Ordinal));
    }
}
