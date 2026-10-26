using Rpsp.Core.Detectors;
using Rpsp.Core.Observations;
using Rpsp.Evaluation.Scenarios;
using Xunit;

namespace Rpsp.Evaluation.Tests;

public sealed class ReplayScenarioCatalogTests
{
    [Fact]
    public void AllCatalogScenariosReturnNonEmptyTraces()
    {
        Assert.All(
            ReplayScenarioCatalog.All,
            static scenario => Assert.NotEmpty(scenario.Trace.Frames));
    }

    [Fact]
    public void AllCatalogScenarioObservationsHaveTimestamps()
    {
        Assert.All(
            ReplayScenarioCatalog.All.SelectMany(static scenario => scenario.Trace.Frames),
            static frame =>
            {
                Assert.NotEqual(default, frame.Timestamp);
                Assert.Equal(frame.Timestamp, frame.Observation.ObservedAt);
            });
    }

    [Fact]
    public async Task ReplaySourceReturnsFramesInChronologicalOrder()
    {
        foreach (ReplayScenario scenario in ReplayScenarioCatalog.All)
        {
            var source = new InMemoryTraceReplaySource(scenario.Trace);
            IReadOnlyList<RadioObservation> observations = await CollectAsync(source.ReplayAsync());

            Assert.Equal(
                observations.OrderBy(static observation => observation.ObservedAt),
                observations);
        }
    }

    [Fact]
    public void ScenarioCatalogDoesNotExposeSensitiveIdentifierTerms()
    {
        string[] sensitiveTerms =
        [
            string.Concat("IM", "SI"),
            string.Concat("IM", "EI"),
            string.Concat("MS", "IS", "DN"),
            string.Concat("SU", "PI"),
            string.Concat("SU", "CI")
        ];
        string[] textualValues = ReplayScenarioCatalog.All
            .SelectMany(GetTextualValues)
            .ToArray();

        foreach (string sensitiveTerm in sensitiveTerms)
        {
            Assert.DoesNotContain(
                textualValues,
                value => value.Contains(sensitiveTerm, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task StationaryHandoverDetectorMatchesScenarioExpectations()
    {
        var detector = new StationaryHandoverAnomalyDetector();

        foreach (ReplayScenario scenario in ReplayScenarioCatalog.All.Where(static scenario => scenario.HasStationaryHandoverExpectation))
        {
            var source = new InMemoryTraceReplaySource(scenario.Trace);
            StationaryHandoverDetectionResult result = await detector.AnalyzeAsync(source.ReplayAsync());

            Assert.Equal(scenario.ExpectedStationaryHandoverSuppression, result.IsSuppressed);

            if (scenario.MinimumStationaryHandoverScore is double minimumScore)
            {
                Assert.True(
                    result.RiskScore.Value >= minimumScore,
                    $"{scenario.Kind} score {result.RiskScore.Value} should be >= {minimumScore}.");
            }

            if (scenario.MaximumStationaryHandoverScore is double maximumScore)
            {
                Assert.True(
                    result.RiskScore.Value <= maximumScore,
                    $"{scenario.Kind} score {result.RiskScore.Value} should be <= {maximumScore}.");
            }

            if (scenario.ExpectedStationaryHandoverSeverity is { } expectedSeverity)
            {
                Assert.Equal(expectedSeverity, result.RiskScore.Severity);
            }
        }
    }

    [Fact]
    public async Task RatDowngradeDetectorMatchesScenarioExpectations()
    {
        var detector = new RatDowngradeRiskDetector();

        foreach (ReplayScenario scenario in ReplayScenarioCatalog.All.Where(static scenario => scenario.HasRatDowngradeExpectation))
        {
            var source = new InMemoryTraceReplaySource(scenario.Trace);
            RatDowngradeDetectionResult result = await detector.AnalyzeAsync(source.ReplayAsync());

            Assert.Equal(scenario.ExpectedRatDowngradeSuppression, result.IsSuppressed);

            if (scenario.MinimumRatDowngradeScore is double minimumScore)
            {
                Assert.True(
                    result.RiskScore.Value >= minimumScore,
                    $"{scenario.Kind} score {result.RiskScore.Value} should be >= {minimumScore}.");
            }

            if (scenario.MaximumRatDowngradeScore is double maximumScore)
            {
                Assert.True(
                    result.RiskScore.Value <= maximumScore,
                    $"{scenario.Kind} score {result.RiskScore.Value} should be <= {maximumScore}.");
            }

            if (scenario.ExpectedRatDowngradeSeverity is { } expectedSeverity)
            {
                Assert.Equal(expectedSeverity, result.RiskScore.Severity);
            }
        }
    }

    [Fact]
    public async Task ScenarioReplayIsDeterministic()
    {
        var detector = new StationaryHandoverAnomalyDetector();

        foreach (ReplayScenario scenario in ReplayScenarioCatalog.All.Where(static scenario => scenario.HasStationaryHandoverExpectation))
        {
            var source = new InMemoryTraceReplaySource(scenario.Trace);
            StationaryHandoverDetectionResult first = await detector.AnalyzeAsync(source.ReplayAsync());
            StationaryHandoverDetectionResult second = await detector.AnalyzeAsync(source.ReplayAsync());

            Assert.Equal(first.RiskScore, second.RiskScore);
            Assert.Equal(first.IsSuppressed, second.IsSuppressed);
            Assert.Equal(first.SuppressionReason, second.SuppressionReason);
            Assert.Equal(first.Evidence.BundleId, second.Evidence.BundleId);
        }
    }

    private static IEnumerable<string> GetTextualValues(ReplayScenario scenario)
    {
        yield return scenario.Kind.ToString();
        yield return scenario.Name;
        yield return scenario.Description;

        foreach (TraceFrame frame in scenario.Trace.Frames)
        {
            yield return frame.Observation.ObservationId;

            foreach (CellSnapshot cell in frame.Observation.CellSnapshots)
            {
                if (cell.Identifier?.NetworkCellId is { } networkCellId)
                {
                    yield return networkCellId;
                }
            }
        }
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
