using Rpsp.Core.Detectors;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;
using Rpsp.Evaluation.Scenarios;
using Xunit;

namespace Rpsp.Evaluation.Tests;

public sealed class CompositeRiskReplayTests
{
    [Fact]
    public async Task ExistingReplayScenarioCanFeedCompositeAssessment()
    {
        CompositeRiskAssessment assessment = await AssessScenarioAsync(ReplayScenarioKind.StationarySuspiciousChurn);

        Assert.True(assessment.RiskScore.Value >= 0.6D);
        Assert.Equal("StationaryHandoverAnomalyDetector", assessment.HighestContributingDetector);
        Assert.Equal(2, assessment.TotalDetectorCount);
    }

    [Fact]
    public async Task CompositeReplayAssessmentIsDeterministic()
    {
        CompositeRiskAssessment first = await AssessScenarioAsync(ReplayScenarioKind.SuspiciousLteToUmtsDowngrade);
        CompositeRiskAssessment second = await AssessScenarioAsync(ReplayScenarioKind.SuspiciousLteToUmtsDowngrade);

        Assert.Equal(first.RiskScore, second.RiskScore);
        Assert.Equal(first.HighestContributingDetector, second.HighestContributingDetector);
        Assert.Equal(first.BaseScore, second.BaseScore);
        Assert.Equal(first.CorrelationBoost, second.CorrelationBoost);
        Assert.Equal(first.Evidence.BundleId, second.Evidence.BundleId);
    }

    private static async Task<CompositeRiskAssessment> AssessScenarioAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
        var source = new InMemoryTraceReplaySource(scenario.Trace);
        var stationaryDetector = new StationaryHandoverAnomalyDetector();
        var ratDetector = new RatDowngradeRiskDetector();
        var scorer = new CompositeRiskScorer();

        IReadOnlyList<RadioObservation> observations = await CollectAsync(source.ReplayAsync()).ConfigureAwait(false);
        StationaryHandoverDetectionResult stationaryResult = stationaryDetector.Analyze(observations);
        RatDowngradeDetectionResult ratResult = ratDetector.Analyze(observations);

        return scorer.Assess(stationaryResult, ratResult);
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
