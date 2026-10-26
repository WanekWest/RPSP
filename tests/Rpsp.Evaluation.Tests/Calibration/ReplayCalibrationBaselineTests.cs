using Rpsp.Core.Detectors;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;
using Rpsp.Evaluation.Scenarios;
using Xunit;

namespace Rpsp.Evaluation.Tests.Calibration;

public sealed class ReplayCalibrationBaselineTests
{
    [Theory]
    [InlineData(ReplayScenarioKind.StationaryStableServingCell, 0.2D, 0.25D)]
    [InlineData(ReplayScenarioKind.MovingUrbanBenignHandover, 0.3D, 0.35D)]
    public async Task StationaryLowRiskScenarioBandsHold(
        ReplayScenarioKind kind,
        double maximumStationaryScore,
        double maximumCompositeScore)
    {
        CalibrationResult result = await AssessScenarioAsync(kind);

        Assert.True(result.Stationary.RiskScore.Value <= maximumStationaryScore);
        Assert.True(result.Composite.RiskScore.Value <= maximumCompositeScore);
    }

    [Theory]
    [InlineData(ReplayScenarioKind.StationarySuspiciousChurn, 0.6D, 0.6D)]
    [InlineData(ReplayScenarioKind.StationaryHighChurn, 0.8D, 0.8D)]
    public async Task StationarySuspiciousScenarioBandsHold(
        ReplayScenarioKind kind,
        double minimumStationaryScore,
        double minimumCompositeScore)
    {
        CalibrationResult result = await AssessScenarioAsync(kind);

        Assert.True(result.Stationary.RiskScore.Value >= minimumStationaryScore);
        Assert.True(result.Composite.RiskScore.Value >= minimumCompositeScore);
    }

    [Theory]
    [InlineData(ReplayScenarioKind.StableLteNoDowngrade, 0.2D, 0.25D)]
    [InlineData(ReplayScenarioKind.NrToLteBenignFallback, 0.3D, 0.35D)]
    [InlineData(ReplayScenarioKind.MovingCoverageLossBenignDowngrade, 0.3D, 0.35D)]
    public async Task RatLowRiskScenarioBandsHold(
        ReplayScenarioKind kind,
        double maximumRatScore,
        double maximumCompositeScore)
    {
        CalibrationResult result = await AssessScenarioAsync(kind);

        Assert.True(result.Rat.RiskScore.Value <= maximumRatScore);
        Assert.True(result.Composite.RiskScore.Value <= maximumCompositeScore);
    }

    [Theory]
    [InlineData(ReplayScenarioKind.SuspiciousLteToUmtsDowngrade, 0.6D, 0.6D)]
    [InlineData(ReplayScenarioKind.HighRiskLteToGsmDowngrade, 0.8D, 0.8D)]
    public async Task RatSuspiciousScenarioBandsHold(
        ReplayScenarioKind kind,
        double minimumRatScore,
        double minimumCompositeScore)
    {
        CalibrationResult result = await AssessScenarioAsync(kind);

        Assert.True(result.Rat.RiskScore.Value >= minimumRatScore);
        Assert.True(result.Composite.RiskScore.Value >= minimumCompositeScore);
    }

    [Theory]
    [InlineData(ReplayScenarioKind.KnownTrustedCellsStable, 0.2D, 0.25D)]
    [InlineData(ReplayScenarioKind.UnknownWeakNeighborCell, 0.3D, 0.35D)]
    [InlineData(ReplayScenarioKind.MovingUnknownStrongCellSuppressed, 0.3D, 0.35D)]
    [InlineData(ReplayScenarioKind.EmptyBaselineSuppressesUnknownCell, 0.25D, 0.3D)]
    [InlineData(ReplayScenarioKind.UnavailableSignalDoesNotTriggerHighRisk, 0.3D, 0.35D)]
    public async Task UnknownStrongCellLowRiskScenarioBandsHold(
        ReplayScenarioKind kind,
        double maximumUnknownScore,
        double maximumCompositeScore)
    {
        UnknownCalibrationResult result = await AssessUnknownScenarioAsync(kind);

        Assert.True(result.Unknown.RiskScore.Value <= maximumUnknownScore);
        Assert.True(result.Composite.RiskScore.Value <= maximumCompositeScore);
    }

    [Theory]
    [InlineData(ReplayScenarioKind.UnknownStrongServingCellStationary, 0.6D, 0.6D)]
    [InlineData(ReplayScenarioKind.RepeatedUnknownVeryStrongServingCellStationary, 0.8D, 0.8D)]
    public async Task UnknownStrongCellSuspiciousScenarioBandsHold(
        ReplayScenarioKind kind,
        double minimumUnknownScore,
        double minimumCompositeScore)
    {
        UnknownCalibrationResult result = await AssessUnknownScenarioAsync(kind);

        Assert.True(result.Unknown.RiskScore.Value >= minimumUnknownScore);
        Assert.True(result.Composite.RiskScore.Value >= minimumCompositeScore);
    }

    [Fact]
    public async Task UnknownStrongNeighborOnlyIsNotHighRiskInCalibration()
    {
        UnknownCalibrationResult result = await AssessUnknownScenarioAsync(ReplayScenarioKind.UnknownStrongNeighborOnly);

        Assert.True(result.Unknown.RiskScore.Value < 0.8D);
        Assert.True(result.Composite.RiskScore.Value < 0.85D);
    }

    [Theory]
    [InlineData(ReplayScenarioKind.StableKnownNeighborGraph, 0.2D, 0.25D)]
    [InlineData(ReplayScenarioKind.SmallNeighborGraphDrift, 0.35D, 0.4D)]
    [InlineData(ReplayScenarioKind.MovingNeighborGraphDriftSuppressed, 0.3D, 0.35D)]
    [InlineData(ReplayScenarioKind.MissingNeighborCellsSuppressed, 0.25D, 0.3D)]
    [InlineData(ReplayScenarioKind.InsufficientNeighborBaselineSuppressed, 0.25D, 0.3D)]
    public async Task NeighborGraphLowRiskScenarioBandsHold(
        ReplayScenarioKind kind,
        double maximumNeighborGraphScore,
        double maximumCompositeScore)
    {
        NeighborGraphCalibrationResult result = await AssessNeighborGraphScenarioAsync(kind);

        Assert.True(result.NeighborGraph.RiskScore.Value <= maximumNeighborGraphScore);
        Assert.True(result.Composite.RiskScore.Value <= maximumCompositeScore);
    }

    [Theory]
    [InlineData(ReplayScenarioKind.StationaryLargeNeighborGraphDrift, 0.6D, 0.6D)]
    [InlineData(ReplayScenarioKind.RepeatedSevereNeighborGraphDrift, 0.8D, 0.8D)]
    [InlineData(ReplayScenarioKind.ServingChangeWithGraphDrift, 0.6D, 0.6D)]
    public async Task NeighborGraphSuspiciousScenarioBandsHold(
        ReplayScenarioKind kind,
        double minimumNeighborGraphScore,
        double minimumCompositeScore)
    {
        NeighborGraphCalibrationResult result = await AssessNeighborGraphScenarioAsync(kind);

        Assert.True(result.NeighborGraph.RiskScore.Value >= minimumNeighborGraphScore);
        Assert.True(result.Composite.RiskScore.Value >= minimumCompositeScore);
    }

    [Fact]
    public async Task CombinedSuspiciousContributionsIncreaseCompositeScoreWithinBounds()
    {
        StationaryHandoverDetectionResult stationary = await AnalyzeStationaryAsync(
            ReplayScenarioKind.StationarySuspiciousChurn);
        RatDowngradeDetectionResult rat = await AnalyzeRatAsync(
            ReplayScenarioKind.SuspiciousLteToUmtsDowngrade);

        CompositeRiskAssessment composite = new CompositeRiskScorer().Assess(stationary, rat);
        double highestIndividualScore = Math.Max(stationary.RiskScore.Value, rat.RiskScore.Value);

        Assert.True(composite.RiskScore.Value > highestIndividualScore);
        Assert.InRange(composite.RiskScore.Value, 0D, 1D);
    }

    [Fact]
    public async Task CombinedSuspiciousMultiDetectorContributionsIncreaseCompositeScoreWithinBounds()
    {
        StationaryHandoverDetectionResult stationary = await AnalyzeStationaryAsync(
            ReplayScenarioKind.StationarySuspiciousChurn);
        RatDowngradeDetectionResult rat = await AnalyzeRatAsync(
            ReplayScenarioKind.SuspiciousLteToUmtsDowngrade);
        UnknownStrongCellDetectionResult unknown = await AnalyzeUnknownAsync(
            ReplayScenarioKind.UnknownStrongServingCellStationary);

        CompositeRiskAssessment composite = new CompositeRiskScorer().Assess(
        [
            DetectorRiskContribution.FromStationaryHandover(stationary),
            DetectorRiskContribution.FromRatDowngrade(rat),
            DetectorRiskContribution.FromUnknownStrongCell(unknown)
        ]);
        double highestIndividualScore = Math.Max(
            stationary.RiskScore.Value,
            Math.Max(rat.RiskScore.Value, unknown.RiskScore.Value));

        Assert.True(stationary.RiskScore.Value >= 0.6D);
        Assert.True(rat.RiskScore.Value >= 0.6D);
        Assert.True(unknown.RiskScore.Value >= 0.6D);
        Assert.True(composite.RiskScore.Value > highestIndividualScore);
        Assert.InRange(composite.RiskScore.Value, 0D, 1D);
    }

    [Fact]
    public async Task CombinedAllDetectorContributionsIncreaseCompositeScoreWithinBounds()
    {
        StationaryHandoverDetectionResult stationary = await AnalyzeStationaryAsync(
            ReplayScenarioKind.StationarySuspiciousChurn);
        RatDowngradeDetectionResult rat = await AnalyzeRatAsync(
            ReplayScenarioKind.SuspiciousLteToUmtsDowngrade);
        UnknownStrongCellDetectionResult unknown = await AnalyzeUnknownAsync(
            ReplayScenarioKind.UnknownStrongServingCellStationary);
        NeighborGraphDetectionResult neighborGraph = await AnalyzeNeighborGraphAsync(
            ReplayScenarioKind.StationaryLargeNeighborGraphDrift);

        CompositeRiskAssessment composite = new CompositeRiskScorer().Assess(
        [
            DetectorRiskContribution.FromStationaryHandover(stationary),
            DetectorRiskContribution.FromRatDowngrade(rat),
            DetectorRiskContribution.FromUnknownStrongCell(unknown),
            DetectorRiskContribution.FromNeighborGraph(neighborGraph)
        ]);
        double highestIndividualScore = Math.Max(
            Math.Max(stationary.RiskScore.Value, rat.RiskScore.Value),
            Math.Max(unknown.RiskScore.Value, neighborGraph.RiskScore.Value));

        Assert.True(stationary.RiskScore.Value >= 0.6D);
        Assert.True(rat.RiskScore.Value >= 0.6D);
        Assert.True(unknown.RiskScore.Value >= 0.6D);
        Assert.True(neighborGraph.RiskScore.Value >= 0.6D);
        Assert.True(composite.RiskScore.Value > highestIndividualScore);
        Assert.InRange(composite.RiskScore.Value, 0D, 1D);
    }

    private static async Task<CalibrationResult> AssessScenarioAsync(ReplayScenarioKind kind)
    {
        IReadOnlyList<RadioObservation> observations = await CollectScenarioAsync(kind).ConfigureAwait(false);
        var stationaryDetector = new StationaryHandoverAnomalyDetector();
        var ratDetector = new RatDowngradeRiskDetector();
        StationaryHandoverDetectionResult stationary = stationaryDetector.Analyze(observations);
        RatDowngradeDetectionResult rat = ratDetector.Analyze(observations);
        CompositeRiskAssessment composite = new CompositeRiskScorer().Assess(stationary, rat);

        return new CalibrationResult(stationary, rat, composite);
    }

    private static async Task<StationaryHandoverDetectionResult> AnalyzeStationaryAsync(ReplayScenarioKind kind)
    {
        IReadOnlyList<RadioObservation> observations = await CollectScenarioAsync(kind).ConfigureAwait(false);

        return new StationaryHandoverAnomalyDetector().Analyze(observations);
    }

    private static async Task<RatDowngradeDetectionResult> AnalyzeRatAsync(ReplayScenarioKind kind)
    {
        IReadOnlyList<RadioObservation> observations = await CollectScenarioAsync(kind).ConfigureAwait(false);

        return new RatDowngradeRiskDetector().Analyze(observations);
    }

    private static async Task<UnknownStrongCellDetectionResult> AnalyzeUnknownAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
        IReadOnlyList<RadioObservation> observations = await CollectScenarioAsync(kind).ConfigureAwait(false);

        return new UnknownStrongCellDetector().Analyze(observations, RequireBaseline(scenario));
    }

    private static async Task<NeighborGraphDetectionResult> AnalyzeNeighborGraphAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
        IReadOnlyList<RadioObservation> observations = await CollectScenarioAsync(kind).ConfigureAwait(false);

        return new NeighborGraphAnomalyDetector().Analyze(observations, RequireBaseline(scenario));
    }

    private static async Task<UnknownCalibrationResult> AssessUnknownScenarioAsync(ReplayScenarioKind kind)
    {
        UnknownStrongCellDetectionResult unknown = await AnalyzeUnknownAsync(kind).ConfigureAwait(false);
        CompositeRiskAssessment composite = new CompositeRiskScorer().Assess(
        [
            DetectorRiskContribution.FromUnknownStrongCell(unknown)
        ]);

        return new UnknownCalibrationResult(unknown, composite);
    }

    private static async Task<NeighborGraphCalibrationResult> AssessNeighborGraphScenarioAsync(ReplayScenarioKind kind)
    {
        NeighborGraphDetectionResult neighborGraph = await AnalyzeNeighborGraphAsync(kind).ConfigureAwait(false);
        CompositeRiskAssessment composite = new CompositeRiskScorer().Assess(
        [
            DetectorRiskContribution.FromNeighborGraph(neighborGraph)
        ]);

        return new NeighborGraphCalibrationResult(neighborGraph, composite);
    }

    private static async Task<IReadOnlyList<RadioObservation>> CollectScenarioAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
        var source = new InMemoryTraceReplaySource(scenario.Trace);
        var observations = new List<RadioObservation>();

        await foreach (RadioObservation observation in source.ReplayAsync().ConfigureAwait(false))
        {
            observations.Add(observation);
        }

        return observations;
    }

    private sealed record CalibrationResult(
        StationaryHandoverDetectionResult Stationary,
        RatDowngradeDetectionResult Rat,
        CompositeRiskAssessment Composite);

    private static Core.Baselines.BaselineProfile RequireBaseline(ReplayScenario scenario)
    {
        return scenario.Baseline
            ?? throw new InvalidOperationException($"Scenario {scenario.Kind} does not define a baseline.");
    }

    private sealed record UnknownCalibrationResult(
        UnknownStrongCellDetectionResult Unknown,
        CompositeRiskAssessment Composite);

    private sealed record NeighborGraphCalibrationResult(
        NeighborGraphDetectionResult NeighborGraph,
        CompositeRiskAssessment Composite);
}
