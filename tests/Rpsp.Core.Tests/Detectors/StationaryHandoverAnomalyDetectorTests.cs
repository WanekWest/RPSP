using Rpsp.Core.Detectors;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;
using Xunit;

namespace Rpsp.Core.Tests.Detectors;

public sealed class StationaryHandoverAnomalyDetectorTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StationaryStableServingCellDoesNotTriggerAnomaly()
    {
        var detector = new StationaryHandoverAnomalyDetector();
        RadioObservation[] observations = CreateTrace(
            DeviceMotionState.Stationary,
            ["A", "A", "A", "A", "A"],
            TimeSpan.FromSeconds(20D));

        StationaryHandoverDetectionResult result = detector.Analyze(observations);

        Assert.Null(result.Event);
        Assert.Equal(RiskSeverity.None, result.RiskScore.Severity);
        Assert.Equal(WarningLevel.None, result.WarningDecision.Level);
        Assert.True(result.RiskScore.Value <= 0.1D);
        Assert.Equal("suppressed:no-serving-cell-churn", result.SuppressionReason);
    }

    [Fact]
    public void StationarySuspiciousCellChurnTriggersAnomaly()
    {
        var detector = new StationaryHandoverAnomalyDetector();
        RadioObservation[] observations = CreateTrace(
            DeviceMotionState.Stationary,
            ["A", "B", "A", "C", "B"],
            TimeSpan.FromSeconds(30D));

        StationaryHandoverDetectionResult result = detector.Analyze(observations);

        Assert.NotNull(result.Event);
        Assert.Equal(AnomalyType.StationaryHandover, result.Event.Type);
        Assert.True(result.RiskScore.Value >= 0.6D);
        Assert.Equal(RiskSeverity.Medium, result.RiskScore.Severity);
        AssertEvidenceContains(result, "ServingCellTransitionCount", "4");
        AssertEvidenceContains(result, "DistinctServingCellCount", "3");
    }

    [Fact]
    public void StationaryHighChurnProducesHighSeverity()
    {
        var detector = new StationaryHandoverAnomalyDetector();
        RadioObservation[] observations = CreateTrace(
            DeviceMotionState.Stationary,
            ["A", "B", "C", "D", "A", "B", "C"],
            TimeSpan.FromSeconds(20D));

        StationaryHandoverDetectionResult result = detector.Analyze(observations);

        Assert.NotNull(result.Event);
        Assert.True(result.RiskScore.Value >= 0.8D);
        Assert.Equal(RiskSeverity.High, result.RiskScore.Severity);
        Assert.Equal(WarningLevel.Warning, result.WarningDecision.Level);
    }

    [Theory]
    [InlineData(DeviceMotionState.Moving)]
    [InlineData(DeviceMotionState.InVehicle)]
    public void MovingBenignHandoverIsSuppressed(DeviceMotionState motionState)
    {
        var detector = new StationaryHandoverAnomalyDetector();
        RadioObservation[] observations = CreateTrace(
            motionState,
            ["A", "B", "C", "D", "A", "B"],
            TimeSpan.FromSeconds(20D));

        StationaryHandoverDetectionResult result = detector.Analyze(observations);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:benign-motion-state", result.SuppressionReason);
        Assert.True(result.RiskScore.Value <= 0.1D);
        AssertEvidenceContains(result, "SuppressionReason", "benign-motion-state");
    }

    [Fact]
    public void ColdStartIsSuppressed()
    {
        var detector = new StationaryHandoverAnomalyDetector();
        RadioObservation[] observations = CreateTrace(
            DeviceMotionState.Stationary,
            ["A", "B", "A"],
            TimeSpan.FromSeconds(20D));

        StationaryHandoverDetectionResult result = detector.Analyze(observations);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:cold-start-observation-count", result.SuppressionReason);
        AssertEvidenceContains(result, "SuppressionReason", "cold-start-observation-count");
    }

    [Fact]
    public void IndoorFadingWithoutServingCellChurnIsSuppressed()
    {
        var detector = new StationaryHandoverAnomalyDetector();
        RadioObservation[] observations =
        [
            CreateObservation("obs-0", Start, DeviceMotionState.Stationary, "A", -80D),
            CreateObservation("obs-1", Start.AddSeconds(30D), DeviceMotionState.Stationary, "A", -92D),
            CreateObservation("obs-2", Start.AddSeconds(60D), DeviceMotionState.Stationary, "A", -104D),
            CreateObservation("obs-3", Start.AddSeconds(90D), DeviceMotionState.Stationary, "A", -110D),
            CreateObservation("obs-4", Start.AddSeconds(120D), DeviceMotionState.Stationary, "A", -115D)
        ];

        StationaryHandoverDetectionResult result = detector.Analyze(observations);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:no-serving-cell-churn", result.SuppressionReason);
        Assert.True(result.RiskScore.Value <= 0.1D);
    }

    [Fact]
    public void RiskScoreIsAlwaysClampedToValidRange()
    {
        var detector = new StationaryHandoverAnomalyDetector(
            new StationaryHandoverDetectorOptions
            {
                SuspiciousTransitionCount = 1,
                HighRiskTransitionCount = 1,
                SuspiciousDistinctServingCells = 1,
                HighRiskDistinctServingCells = 1,
                SuspiciousTransitionsPerMinute = 0.1D,
                HighRiskTransitionsPerMinute = 0.1D
            });
        RadioObservation[] observations = CreateTrace(
            DeviceMotionState.Stationary,
            ["A", "B", "C", "D", "A", "B", "C", "D"],
            TimeSpan.FromSeconds(10D));

        StationaryHandoverDetectionResult result = detector.Analyze(observations);

        Assert.InRange(result.RiskScore.Value, 0D, 1D);
        Assert.True(result.RiskScore.Value <= 1D);
    }

    private static void AssertEvidenceContains(
        StationaryHandoverDetectionResult result,
        string evidenceName,
        string expectedDescriptionFragment)
    {
        EvidenceItem evidenceItem = Assert.Single(
            result.Evidence.Items,
            item => string.Equals(item.Name, evidenceName, StringComparison.Ordinal));

        Assert.Contains(expectedDescriptionFragment, evidenceItem.Description, StringComparison.Ordinal);
    }

    private static RadioObservation[] CreateTrace(
        DeviceMotionState motionState,
        IReadOnlyList<string> cellIds,
        TimeSpan step)
    {
        return cellIds
            .Select((cellId, index) => CreateObservation(
                string.Create(null, $"obs-{index}"),
                Start.AddTicks(step.Ticks * index),
                motionState,
                cellId,
                -82D))
            .ToArray();
    }

    private static RadioObservation CreateObservation(
        string observationId,
        DateTimeOffset observedAt,
        DeviceMotionState motionState,
        string cellId,
        double rsrpDbm)
    {
        var serving = new CellSnapshot(
            CellRole.Serving,
            RadioAccessTechnology.Lte,
            new CellIdentifier(new Plmn("001", "01"), cellId, PhysicalCellId: 42),
            new SignalSnapshot(RsrpDbm: rsrpDbm),
            ObservationAvailability.Available,
            IsRegistered: true);
        var deviceMotion = new DeviceMotionSnapshot(motionState, confidence: 0.95D);

        return new RadioObservation(
            observationId,
            observedAt,
            ObservationSourceKind.Replay,
            [serving],
            deviceMotion: deviceMotion);
    }
}
