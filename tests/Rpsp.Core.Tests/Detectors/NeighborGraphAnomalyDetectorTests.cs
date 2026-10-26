using Rpsp.Core.Baselines;
using Rpsp.Core.Detectors;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;
using Xunit;

namespace Rpsp.Core.Tests.Detectors;

public sealed class NeighborGraphAnomalyDetectorTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StableKnownNeighborGraphDoesNotTriggerAnomaly()
    {
        var detector = new NeighborGraphAnomalyDetector();
        BaselineProfile baseline = CreateBaseline("neighbor-a", "neighbor-b", "neighbor-c");
        RadioObservation[] observations = CreateNeighborTrace(
            DeviceMotionState.Stationary,
            [
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"])
            ]);

        NeighborGraphDetectionResult result = detector.Analyze(observations, baseline);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:no-neighbor-graph-drift", result.SuppressionReason);
        Assert.Equal(RiskSeverity.None, result.RiskScore.Severity);
        Assert.True(result.RiskScore.Value <= 0.2D);
    }

    [Fact]
    public void SmallNeighborGraphDriftIsNotHighRisk()
    {
        var detector = new NeighborGraphAnomalyDetector();
        BaselineProfile baseline = CreateBaseline("neighbor-a", "neighbor-b", "neighbor-c");
        RadioObservation[] observations = CreateNeighborTrace(
            DeviceMotionState.Stationary,
            [
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-x"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-x"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"])
            ]);

        NeighborGraphDetectionResult result = detector.Analyze(observations, baseline);

        Assert.Null(result.Event);
        Assert.NotEqual(RiskSeverity.High, result.RiskScore.Severity);
        Assert.True(result.RiskScore.Value <= 0.3D);
    }

    [Fact]
    public void StationaryLargeNeighborGraphDriftTriggersSuspiciousScore()
    {
        var detector = new NeighborGraphAnomalyDetector();
        BaselineProfile baseline = CreateBaseline("neighbor-a", "neighbor-b", "neighbor-c");
        RadioObservation[] observations = CreateNeighborTrace(
            DeviceMotionState.Stationary,
            [
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"])
            ]);

        NeighborGraphDetectionResult result = detector.Analyze(observations, baseline);

        Assert.NotNull(result.Event);
        Assert.Equal(AnomalyType.NeighborGraphAnomaly, result.Event.Type);
        Assert.True(result.RiskScore.Value >= 0.6D);
        AssertEvidenceContains(result, "LowSimilarityObservationCount", "2");
        AssertEvidenceContains(result, "MinimumJaccardSimilarity", "0.000");
    }

    [Fact]
    public void RepeatedSevereNeighborGraphDriftProducesHighScore()
    {
        var detector = new NeighborGraphAnomalyDetector();
        BaselineProfile baseline = CreateBaseline("neighbor-a", "neighbor-b", "neighbor-c");
        RadioObservation[] observations = CreateNeighborTrace(
            DeviceMotionState.Stationary,
            [
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"])
            ]);

        NeighborGraphDetectionResult result = detector.Analyze(observations, baseline);

        Assert.NotNull(result.Event);
        Assert.True(result.RiskScore.Value >= 0.8D);
        Assert.Equal(RiskSeverity.High, result.RiskScore.Severity);
        Assert.Equal(WarningLevel.Warning, result.WarningDecision.Level);
    }

    [Theory]
    [InlineData(DeviceMotionState.Moving)]
    [InlineData(DeviceMotionState.InVehicle)]
    public void MovingNeighborGraphDriftIsSuppressed(DeviceMotionState motionState)
    {
        var detector = new NeighborGraphAnomalyDetector();
        BaselineProfile baseline = CreateBaseline("neighbor-a", "neighbor-b", "neighbor-c");
        RadioObservation[] observations = CreateNeighborTrace(
            motionState,
            [
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-b", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-c", ["neighbor-p", "neighbor-q", "neighbor-r"]),
                ("serving-d", ["neighbor-l", "neighbor-m", "neighbor-n"]),
                ("serving-e", ["neighbor-a", "neighbor-b", "neighbor-c"])
            ]);

        NeighborGraphDetectionResult result = detector.Analyze(observations, baseline);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:benign-motion-state", result.SuppressionReason);
        Assert.True(result.RiskScore.Value <= 0.1D);
    }

    [Fact]
    public void MissingNeighborCellsSuppressDetector()
    {
        var detector = new NeighborGraphAnomalyDetector();
        BaselineProfile baseline = CreateBaseline("neighbor-a", "neighbor-b", "neighbor-c");
        RadioObservation[] observations = Enumerable.Range(0, 5)
            .Select(index => CreateObservation(
                string.Create(null, $"obs-{index}"),
                Start.AddSeconds(30D * index),
                DeviceMotionState.Stationary,
                "serving-a",
                []))
            .ToArray();

        NeighborGraphDetectionResult result = detector.Analyze(observations, baseline);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:missing-neighbor-cells", result.SuppressionReason);
        Assert.True(result.RiskScore.Value <= 0.1D);
    }

    [Fact]
    public void InsufficientBaselineSuppressesDetector()
    {
        var detector = new NeighborGraphAnomalyDetector();
        BaselineProfile baseline = CreateBaseline("neighbor-a");
        RadioObservation[] observations = CreateNeighborTrace(
            DeviceMotionState.Stationary,
            [
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"])
            ]);

        NeighborGraphDetectionResult result = detector.Analyze(observations, baseline);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:insufficient-neighbor-baseline", result.SuppressionReason);
        Assert.True(result.RiskScore.Value <= 0.1D);
    }

    [Fact]
    public void ServingChangeWithGraphDriftIncreasesRisk()
    {
        BaselineProfile baseline = CreateBaseline("neighbor-a", "neighbor-b", "neighbor-c");
        RadioObservation[] observations = CreateNeighborTrace(
            DeviceMotionState.Stationary,
            [
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-b", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-b", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"])
            ]);
        NeighborGraphDetectionResult withoutBoost = new NeighborGraphAnomalyDetector(
            new NeighborGraphDetectorOptions { ServingCellChangeBoost = 0D }).Analyze(observations, baseline);
        NeighborGraphDetectionResult withBoost = new NeighborGraphAnomalyDetector().Analyze(observations, baseline);

        Assert.True(withBoost.RiskScore.Value > withoutBoost.RiskScore.Value);
        Assert.True(withBoost.RiskScore.Value >= 0.6D);
        AssertEvidenceContains(withBoost, "ServingCellTransitionCount", "2");
    }

    [Fact]
    public void RiskScoreIsClampedToValidRange()
    {
        var detector = new NeighborGraphAnomalyDetector(
            new NeighborGraphDetectorOptions
            {
                ServingCellChangeBoost = 1D,
                RepeatedDriftBoost = 1D,
                MaximumCorrelationBoost = 1D
            });
        BaselineProfile baseline = CreateBaseline("neighbor-a", "neighbor-b", "neighbor-c");
        RadioObservation[] observations = CreateNeighborTrace(
            DeviceMotionState.Stationary,
            [
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-b", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-c", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-d", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-e", ["neighbor-x", "neighbor-y", "neighbor-z"])
            ]);

        NeighborGraphDetectionResult result = detector.Analyze(observations, baseline);

        Assert.InRange(result.RiskScore.Value, 0D, 1D);
        Assert.Equal(1D, result.RiskScore.Value);
    }

    [Fact]
    public void CompositeScorerCanIncludeNeighborGraphContribution()
    {
        var detector = new NeighborGraphAnomalyDetector();
        var scorer = new CompositeRiskScorer();
        BaselineProfile baseline = CreateBaseline("neighbor-a", "neighbor-b", "neighbor-c");
        RadioObservation[] observations = CreateNeighborTrace(
            DeviceMotionState.Stationary,
            [
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"])
            ]);
        NeighborGraphDetectionResult result = detector.Analyze(observations, baseline);

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            DetectorRiskContribution.FromNeighborGraph(result)
        ]);

        Assert.Equal("NeighborGraphAnomalyDetector", assessment.HighestContributingDetector);
        Assert.True(assessment.RiskScore.Value >= 0.6D);
        Assert.Equal(1, assessment.TotalDetectorCount);
    }

    private static void AssertEvidenceContains(
        NeighborGraphDetectionResult result,
        string evidenceName,
        string expectedDescriptionFragment)
    {
        EvidenceItem evidenceItem = Assert.Single(
            result.Evidence.Items,
            item => string.Equals(item.Name, evidenceName, StringComparison.Ordinal));

        Assert.Contains(expectedDescriptionFragment, evidenceItem.Description, StringComparison.Ordinal);
    }

    private static BaselineProfile CreateBaseline(params string[] neighborCellIds)
    {
        TrustedCellSignature[] trustedCells = neighborCellIds
            .Select(static neighborCellId => new TrustedCellSignature(
                string.Create(null, $"trusted:{neighborCellId}"),
                RadioAccessTechnology.Lte,
                NetworkCellPseudonym: neighborCellId,
                ChannelNumber: 6300))
            .ToArray();

        return new BaselineProfile("baseline", Start.AddDays(-7D), Start, trustedCells);
    }

    private static RadioObservation[] CreateNeighborTrace(
        DeviceMotionState motionState,
        IReadOnlyList<(string ServingCellId, string[] NeighborCellIds)> frames)
    {
        return frames
            .Select((frame, index) => CreateObservation(
                string.Create(null, $"obs-{index}"),
                Start.AddSeconds(30D * index),
                motionState,
                frame.ServingCellId,
                frame.NeighborCellIds))
            .ToArray();
    }

    private static RadioObservation CreateObservation(
        string observationId,
        DateTimeOffset observedAt,
        DeviceMotionState motionState,
        string servingCellId,
        IReadOnlyList<string> neighborCellIds)
    {
        var cells = new List<CellSnapshot>
        {
            CreateCell(CellRole.Serving, servingCellId, isRegistered: true)
        };

        foreach (string neighborCellId in neighborCellIds)
        {
            cells.Add(CreateCell(CellRole.Neighbor, neighborCellId, isRegistered: false));
        }

        var serviceState = new ServiceStateSnapshot(
            IsInService: true,
            VoiceRadioAccessTechnology: RadioAccessTechnology.Lte,
            DataRadioAccessTechnology: RadioAccessTechnology.Lte);
        var deviceMotion = new DeviceMotionSnapshot(motionState, confidence: 0.95D);

        return new RadioObservation(
            observationId,
            observedAt,
            ObservationSourceKind.Replay,
            cells,
            serviceState,
            deviceMotion: deviceMotion);
    }

    private static CellSnapshot CreateCell(CellRole role, string syntheticCellId, bool isRegistered)
    {
        return new CellSnapshot(
            role,
            RadioAccessTechnology.Lte,
            new CellIdentifier(new Plmn("001", "01"), syntheticCellId, ChannelNumber: 6300),
            new SignalSnapshot(RsrpDbm: -84D),
            ObservationAvailability.Available,
            IsRegistered: isRegistered);
    }
}
