using Rpsp.Core.Detectors;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;
using Rpsp.Evaluation.Stress;

namespace Rpsp.Evaluation.Tests.Stress;

internal static class StressTraceTestSupport
{
    public static async Task<IReadOnlyList<RadioObservation>> CollectAsync(SyntheticStressTrace trace)
    {
        var source = new InMemoryTraceReplaySource(trace.Trace);
        var observations = new List<RadioObservation>();

        await foreach (RadioObservation observation in source.ReplayAsync().ConfigureAwait(false))
        {
            observations.Add(observation);
        }

        return observations;
    }

    public static StressAssessment Assess(
        IReadOnlyList<RadioObservation> observations,
        SyntheticStressTrace trace)
    {
        var stationary = new StationaryHandoverAnomalyDetector().Analyze(observations);
        var rat = new RatDowngradeRiskDetector().Analyze(observations);
        var unknown = new UnknownStrongCellDetector().Analyze(observations, trace.Baseline);
        var neighbor = new NeighborGraphAnomalyDetector().Analyze(observations, trace.Baseline);
        CompositeRiskAssessment composite = new CompositeRiskScorer().Assess(
        [
            DetectorRiskContribution.FromStationaryHandover(stationary),
            DetectorRiskContribution.FromRatDowngrade(rat),
            DetectorRiskContribution.FromUnknownStrongCell(unknown),
            DetectorRiskContribution.FromNeighborGraph(neighbor)
        ]);

        return new StressAssessment(stationary, rat, unknown, neighbor, composite);
    }

    public static IReadOnlyList<RadioObservation> Window(
        IReadOnlyList<RadioObservation> observations,
        SyntheticStressWindow window)
    {
        return observations
            .Skip(window.StartIndex)
            .Take(window.ObservationCount)
            .ToArray();
    }

    public static IEnumerable<IReadOnlyList<RadioObservation>> SlidingWindows(
        IReadOnlyList<RadioObservation> observations,
        int windowSize,
        int step = 1)
    {
        for (int start = 0; start + windowSize <= observations.Count; start += step)
        {
            yield return observations.Skip(start).Take(windowSize).ToArray();
        }
    }

    public static SyntheticStressWindow RequireWindow(
        SyntheticStressTrace trace,
        string name)
    {
        return trace.Windows.Single(window => string.Equals(window.Name, name, StringComparison.Ordinal));
    }
}

internal sealed record StressAssessment(
    StationaryHandoverDetectionResult Stationary,
    RatDowngradeDetectionResult Rat,
    UnknownStrongCellDetectionResult Unknown,
    NeighborGraphDetectionResult Neighbor,
    CompositeRiskAssessment Composite)
{
    public double HighestIndividualScore => Math.Max(
        Math.Max(Stationary.RiskScore.Value, Rat.RiskScore.Value),
        Math.Max(Unknown.RiskScore.Value, Neighbor.RiskScore.Value));
}
