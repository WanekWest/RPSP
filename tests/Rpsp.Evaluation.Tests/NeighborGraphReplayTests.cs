using Rpsp.Core.Detectors;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;
using Rpsp.Evaluation.Scenarios;
using Xunit;

namespace Rpsp.Evaluation.Tests;

public sealed class NeighborGraphReplayTests
{
    [Fact]
    public async Task NeighborGraphDetectorMatchesScenarioExpectations()
    {
        var detector = new NeighborGraphAnomalyDetector();

        foreach (ReplayScenario scenario in ReplayScenarioCatalog.All.Where(static scenario => scenario.HasNeighborGraphExpectation))
        {
            Assert.NotNull(scenario.Baseline);

            var source = new InMemoryTraceReplaySource(scenario.Trace);
            NeighborGraphDetectionResult result = await detector
                .AnalyzeAsync(source.ReplayAsync(), scenario.Baseline);

            Assert.Equal(scenario.ExpectedNeighborGraphSuppression, result.IsSuppressed);

            if (scenario.MinimumNeighborGraphScore is double minimumScore)
            {
                Assert.True(
                    result.RiskScore.Value >= minimumScore,
                    $"{scenario.Kind} score {result.RiskScore.Value} should be >= {minimumScore}.");
            }

            if (scenario.MaximumNeighborGraphScore is double maximumScore)
            {
                Assert.True(
                    result.RiskScore.Value <= maximumScore,
                    $"{scenario.Kind} score {result.RiskScore.Value} should be <= {maximumScore}.");
            }

            if (scenario.ExpectedNeighborGraphSeverity is { } expectedSeverity)
            {
                Assert.Equal(expectedSeverity, result.RiskScore.Severity);
            }
        }
    }

    [Fact]
    public async Task NeighborGraphScenarioReplayIsDeterministic()
    {
        var detector = new NeighborGraphAnomalyDetector();

        foreach (ReplayScenario scenario in ReplayScenarioCatalog.All.Where(static scenario => scenario.HasNeighborGraphExpectation))
        {
            Assert.NotNull(scenario.Baseline);

            var source = new InMemoryTraceReplaySource(scenario.Trace);
            NeighborGraphDetectionResult first = await detector
                .AnalyzeAsync(source.ReplayAsync(), scenario.Baseline);
            NeighborGraphDetectionResult second = await detector
                .AnalyzeAsync(source.ReplayAsync(), scenario.Baseline);

            Assert.Equal(first.RiskScore, second.RiskScore);
            Assert.Equal(first.IsSuppressed, second.IsSuppressed);
            Assert.Equal(first.SuppressionReason, second.SuppressionReason);
            Assert.Equal(first.Evidence.BundleId, second.Evidence.BundleId);
        }
    }

    [Fact]
    public async Task ReplayScenarioCanFeedCompositeWithNeighborGraphContribution()
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(ReplayScenarioKind.StationaryLargeNeighborGraphDrift);
        Assert.NotNull(scenario.Baseline);

        var source = new InMemoryTraceReplaySource(scenario.Trace);
        var detector = new NeighborGraphAnomalyDetector();
        var scorer = new CompositeRiskScorer();
        IReadOnlyList<RadioObservation> observations = await CollectAsync(source.ReplayAsync());
        NeighborGraphDetectionResult result = detector.Analyze(observations, scenario.Baseline);

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            DetectorRiskContribution.FromNeighborGraph(result)
        ]);

        Assert.Equal("NeighborGraphAnomalyDetector", assessment.HighestContributingDetector);
        Assert.True(assessment.RiskScore.Value >= 0.6D);
    }

    private static async Task<IReadOnlyList<RadioObservation>> CollectAsync(
        IAsyncEnumerable<RadioObservation> observations)
    {
        var collected = new List<RadioObservation>();

        await foreach (RadioObservation observation in observations.ConfigureAwait(false))
        {
            collected.Add(observation);
        }

        return collected;
    }
}
