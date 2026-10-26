using Rpsp.Core.Detectors;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;
using Rpsp.Evaluation.Scenarios;
using Xunit;

namespace Rpsp.Evaluation.Tests;

public sealed class UnknownStrongCellReplayTests
{
    [Fact]
    public async Task UnknownStrongCellDetectorMatchesScenarioExpectations()
    {
        var detector = new UnknownStrongCellDetector();

        foreach (ReplayScenario scenario in ReplayScenarioCatalog.All.Where(static scenario => scenario.HasUnknownStrongCellExpectation))
        {
            Assert.NotNull(scenario.Baseline);

            var source = new InMemoryTraceReplaySource(scenario.Trace);
            UnknownStrongCellDetectionResult result = await detector
                .AnalyzeAsync(source.ReplayAsync(), scenario.Baseline);

            Assert.Equal(scenario.ExpectedUnknownStrongCellSuppression, result.IsSuppressed);

            if (scenario.MinimumUnknownStrongCellScore is double minimumScore)
            {
                Assert.True(
                    result.RiskScore.Value >= minimumScore,
                    $"{scenario.Kind} score {result.RiskScore.Value} should be >= {minimumScore}.");
            }

            if (scenario.MaximumUnknownStrongCellScore is double maximumScore)
            {
                Assert.True(
                    result.RiskScore.Value <= maximumScore,
                    $"{scenario.Kind} score {result.RiskScore.Value} should be <= {maximumScore}.");
            }

            if (scenario.ExpectedUnknownStrongCellSeverity is { } expectedSeverity)
            {
                Assert.Equal(expectedSeverity, result.RiskScore.Severity);
            }
        }
    }

    [Fact]
    public async Task UnknownStrongCellScenarioReplayIsDeterministic()
    {
        var detector = new UnknownStrongCellDetector();

        foreach (ReplayScenario scenario in ReplayScenarioCatalog.All.Where(static scenario => scenario.HasUnknownStrongCellExpectation))
        {
            Assert.NotNull(scenario.Baseline);

            var source = new InMemoryTraceReplaySource(scenario.Trace);
            UnknownStrongCellDetectionResult first = await detector
                .AnalyzeAsync(source.ReplayAsync(), scenario.Baseline);
            UnknownStrongCellDetectionResult second = await detector
                .AnalyzeAsync(source.ReplayAsync(), scenario.Baseline);

            Assert.Equal(first.RiskScore, second.RiskScore);
            Assert.Equal(first.IsSuppressed, second.IsSuppressed);
            Assert.Equal(first.SuppressionReason, second.SuppressionReason);
            Assert.Equal(first.Evidence.BundleId, second.Evidence.BundleId);
        }
    }

    [Fact]
    public async Task ReplayScenarioCanFeedCompositeWithUnknownStrongCellContribution()
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(ReplayScenarioKind.UnknownStrongServingCellStationary);
        Assert.NotNull(scenario.Baseline);

        var source = new InMemoryTraceReplaySource(scenario.Trace);
        var detector = new UnknownStrongCellDetector();
        var scorer = new CompositeRiskScorer();
        IReadOnlyList<RadioObservation> observations = await CollectAsync(source.ReplayAsync());
        UnknownStrongCellDetectionResult result = detector.Analyze(observations, scenario.Baseline);

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            DetectorRiskContribution.FromUnknownStrongCell(result)
        ]);

        Assert.Equal("UnknownStrongCellDetector", assessment.HighestContributingDetector);
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
