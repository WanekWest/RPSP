using Rpsp.Core.Baselines;
using Rpsp.Core.Detectors;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;
using Xunit;

namespace Rpsp.Core.Tests.Detectors;

public sealed class UnknownStrongCellDetectorTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void KnownTrustedCellsDoNotTriggerAnomaly()
    {
        var detector = new UnknownStrongCellDetector();
        BaselineProfile baseline = CreateBaseline("synthetic-known-a", "synthetic-known-b");
        RadioObservation[] observations = CreateKnownServingWithNeighborTrace();

        UnknownStrongCellDetectionResult result = detector.Analyze(observations, baseline);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:no-unknown-cells", result.SuppressionReason);
        Assert.Equal(RiskSeverity.None, result.RiskScore.Severity);
        Assert.True(result.RiskScore.Value <= 0.1D);
    }

    [Fact]
    public void UnknownWeakNeighborCellDoesNotTriggerHighRisk()
    {
        var detector = new UnknownStrongCellDetector();
        BaselineProfile baseline = CreateBaseline("synthetic-known-a", "synthetic-known-b");
        RadioObservation[] observations = CreateServingWithNeighborTrace(
            "synthetic-known-a",
            "synthetic-unknown-weak",
            DeviceMotionState.Stationary,
            servingRsrpDbm: -82D,
            neighborRsrpDbm: -101D);

        UnknownStrongCellDetectionResult result = detector.Analyze(observations, baseline);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:no-strong-signal-evidence", result.SuppressionReason);
        Assert.NotEqual(RiskSeverity.High, result.RiskScore.Severity);
        Assert.True(result.RiskScore.Value <= 0.1D);
    }

    [Fact]
    public void UnknownStrongServingCellStationaryTriggersSuspiciousScore()
    {
        var detector = new UnknownStrongCellDetector();
        BaselineProfile baseline = CreateBaseline("synthetic-known-a", "synthetic-known-b");
        RadioObservation[] observations = CreateServingTrace(
            DeviceMotionState.Stationary,
            [
                ("synthetic-known-a", -83D),
                ("synthetic-unknown-strong", -74D),
                ("synthetic-unknown-strong", -73D),
                ("synthetic-unknown-strong", -72D),
                ("synthetic-known-a", -82D)
            ]);

        UnknownStrongCellDetectionResult result = detector.Analyze(observations, baseline);

        Assert.NotNull(result.Event);
        Assert.Equal(AnomalyType.NewStrongUnknownCell, result.Event.Type);
        Assert.True(result.RiskScore.Value >= 0.6D);
        Assert.Equal(RiskSeverity.Medium, result.RiskScore.Severity);
        Assert.Equal(WarningLevel.Caution, result.WarningDecision.Level);
        AssertEvidenceContains(result, "UnknownStrongServingAppearances", "3");
        AssertEvidenceContains(result, "RepeatedUnknownStrongServingCellObserved", "True");
    }

    [Fact]
    public void RepeatedVeryStrongUnknownServingCellStationaryProducesHighSeverity()
    {
        var detector = new UnknownStrongCellDetector();
        BaselineProfile baseline = CreateBaseline("synthetic-known-a", "synthetic-known-b");
        RadioObservation[] observations = CreateServingTrace(
            DeviceMotionState.Stationary,
            [
                ("synthetic-known-a", -83D),
                ("synthetic-unknown-very-strong", -63D),
                ("synthetic-unknown-very-strong", -62D),
                ("synthetic-unknown-very-strong", -64D),
                ("synthetic-unknown-very-strong", -63D)
            ]);

        UnknownStrongCellDetectionResult result = detector.Analyze(observations, baseline);

        Assert.NotNull(result.Event);
        Assert.True(result.RiskScore.Value >= 0.8D);
        Assert.Equal(RiskSeverity.High, result.RiskScore.Severity);
        Assert.Equal(WarningLevel.Warning, result.WarningDecision.Level);
    }

    [Fact]
    public void UnknownStrongNeighborOnlyIsNotHighRisk()
    {
        var detector = new UnknownStrongCellDetector();
        BaselineProfile baseline = CreateBaseline("synthetic-known-a", "synthetic-known-b");
        RadioObservation[] observations = CreateServingWithNeighborTrace(
            "synthetic-known-a",
            "synthetic-unknown-neighbor-strong",
            DeviceMotionState.Stationary,
            servingRsrpDbm: -84D,
            neighborRsrpDbm: -69D);

        UnknownStrongCellDetectionResult result = detector.Analyze(observations, baseline);

        Assert.Null(result.Event);
        Assert.False(result.IsSuppressed);
        Assert.NotEqual(RiskSeverity.High, result.RiskScore.Severity);
        Assert.True(result.RiskScore.Value < 0.6D);
        AssertEvidenceContains(result, "UnknownStrongNeighborAppearances", "5");
    }

    [Theory]
    [InlineData(DeviceMotionState.Moving)]
    [InlineData(DeviceMotionState.InVehicle)]
    public void MovingUnknownStrongCellIsSuppressed(DeviceMotionState motionState)
    {
        var detector = new UnknownStrongCellDetector();
        BaselineProfile baseline = CreateBaseline("synthetic-known-a", "synthetic-known-b");
        RadioObservation[] observations = CreateServingTrace(
            motionState,
            [
                ("synthetic-known-a", -83D),
                ("synthetic-unknown-strong", -70D),
                ("synthetic-unknown-strong", -69D),
                ("synthetic-unknown-strong", -70D),
                ("synthetic-known-b", -82D)
            ]);

        UnknownStrongCellDetectionResult result = detector.Analyze(observations, baseline);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:benign-motion-state", result.SuppressionReason);
        Assert.True(result.RiskScore.Value <= 0.1D);
    }

    [Fact]
    public void EmptyBaselineSuppressesDetector()
    {
        var detector = new UnknownStrongCellDetector();
        BaselineProfile baseline = CreateBaseline();
        RadioObservation[] observations = CreateServingTrace(
            DeviceMotionState.Stationary,
            [
                ("synthetic-unknown-strong", -72D),
                ("synthetic-unknown-strong", -72D),
                ("synthetic-unknown-strong", -71D),
                ("synthetic-unknown-strong", -72D),
                ("synthetic-unknown-strong", -71D)
            ]);

        UnknownStrongCellDetectionResult result = detector.Analyze(observations, baseline);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:insufficient-trusted-baseline", result.SuppressionReason);
        Assert.True(result.RiskScore.Value <= 0.1D);
    }

    [Fact]
    public void UnavailableSignalDoesNotTriggerHighRisk()
    {
        var detector = new UnknownStrongCellDetector();
        BaselineProfile baseline = CreateBaseline("synthetic-known-a", "synthetic-known-b");
        RadioObservation[] observations = Enumerable.Range(0, 5)
            .Select(index => CreateObservation(
                string.Create(null, $"obs-{index}"),
                Start.AddSeconds(30D * index),
                DeviceMotionState.Stationary,
                [CreateCell(CellRole.Serving, "synthetic-unknown-unavailable", null, isRegistered: true)]))
            .ToArray();

        UnknownStrongCellDetectionResult result = detector.Analyze(observations, baseline);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:no-strong-signal-evidence", result.SuppressionReason);
        Assert.NotEqual(RiskSeverity.High, result.RiskScore.Severity);
    }

    [Fact]
    public void RiskScoreIsAlwaysClampedToValidRange()
    {
        var detector = new UnknownStrongCellDetector(
            new UnknownStrongCellDetectorOptions
            {
                SuspiciousUnknownStrongServingAppearances = 1,
                HighRiskUnknownStrongServingAppearances = 1,
                StrongSignalScoreWeight = 1D,
                RepeatedAppearanceScoreWeight = 1D
            });
        BaselineProfile baseline = CreateBaseline("synthetic-known-a", "synthetic-known-b");
        RadioObservation[] observations = CreateServingTrace(
            DeviceMotionState.Stationary,
            [
                ("synthetic-unknown-very-strong", -60D),
                ("synthetic-unknown-very-strong", -60D),
                ("synthetic-unknown-very-strong", -60D),
                ("synthetic-unknown-very-strong", -60D),
                ("synthetic-unknown-very-strong", -60D)
            ]);

        UnknownStrongCellDetectionResult result = detector.Analyze(observations, baseline);

        Assert.InRange(result.RiskScore.Value, 0D, 1D);
        Assert.Equal(1D, result.RiskScore.Value);
    }

    [Fact]
    public void CompositeScorerCanIncludeUnknownStrongCellContribution()
    {
        var detector = new UnknownStrongCellDetector();
        var scorer = new CompositeRiskScorer();
        BaselineProfile baseline = CreateBaseline("synthetic-known-a", "synthetic-known-b");
        RadioObservation[] observations = CreateServingTrace(
            DeviceMotionState.Stationary,
            [
                ("synthetic-known-a", -83D),
                ("synthetic-unknown-strong", -74D),
                ("synthetic-unknown-strong", -73D),
                ("synthetic-unknown-strong", -72D),
                ("synthetic-known-a", -82D)
            ]);
        UnknownStrongCellDetectionResult result = detector.Analyze(observations, baseline);

        CompositeRiskAssessment assessment = scorer.Assess(
        [
            DetectorRiskContribution.FromUnknownStrongCell(result)
        ]);

        Assert.Equal("UnknownStrongCellDetector", assessment.HighestContributingDetector);
        Assert.True(assessment.RiskScore.Value >= 0.6D);
        Assert.Equal(1, assessment.TotalDetectorCount);
    }

    private static void AssertEvidenceContains(
        UnknownStrongCellDetectionResult result,
        string evidenceName,
        string expectedDescriptionFragment)
    {
        EvidenceItem evidenceItem = Assert.Single(
            result.Evidence.Items,
            item => string.Equals(item.Name, evidenceName, StringComparison.Ordinal));

        Assert.Contains(expectedDescriptionFragment, evidenceItem.Description, StringComparison.Ordinal);
    }

    private static BaselineProfile CreateBaseline(params string[] syntheticCellIds)
    {
        TrustedCellSignature[] trustedCells = syntheticCellIds
            .Select(static syntheticCellId => new TrustedCellSignature(
                string.Create(null, $"trusted:{syntheticCellId}"),
                RadioAccessTechnology.Lte,
                NetworkCellPseudonym: syntheticCellId,
                ChannelNumber: 6300))
            .ToArray();

        return new BaselineProfile("baseline", Start.AddDays(-7D), Start, trustedCells);
    }

    private static RadioObservation[] CreateKnownServingWithNeighborTrace()
    {
        return CreateServingWithNeighborTrace(
            "synthetic-known-a",
            "synthetic-known-b",
            DeviceMotionState.Stationary,
            servingRsrpDbm: -82D,
            neighborRsrpDbm: -86D);
    }

    private static RadioObservation[] CreateServingWithNeighborTrace(
        string servingCellId,
        string neighborCellId,
        DeviceMotionState motionState,
        double servingRsrpDbm,
        double neighborRsrpDbm)
    {
        return Enumerable.Range(0, 5)
            .Select(index => CreateObservation(
                string.Create(null, $"obs-{index}"),
                Start.AddSeconds(30D * index),
                motionState,
                [
                    CreateCell(CellRole.Serving, servingCellId, servingRsrpDbm, isRegistered: true),
                    CreateCell(CellRole.Neighbor, neighborCellId, neighborRsrpDbm, isRegistered: false)
                ]))
            .ToArray();
    }

    private static RadioObservation[] CreateServingTrace(
        DeviceMotionState motionState,
        IReadOnlyList<(string CellId, double? RsrpDbm)> cells)
    {
        return cells
            .Select((cell, index) => CreateObservation(
                string.Create(null, $"obs-{index}"),
                Start.AddSeconds(30D * index),
                motionState,
                [CreateCell(CellRole.Serving, cell.CellId, cell.RsrpDbm, isRegistered: true)]))
            .ToArray();
    }

    private static RadioObservation CreateObservation(
        string observationId,
        DateTimeOffset observedAt,
        DeviceMotionState motionState,
        IReadOnlyList<CellSnapshot> cells)
    {
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

    private static CellSnapshot CreateCell(
        CellRole role,
        string syntheticCellId,
        double? rsrpDbm,
        bool isRegistered)
    {
        return new CellSnapshot(
            role,
            RadioAccessTechnology.Lte,
            new CellIdentifier(new Plmn("001", "01"), syntheticCellId, ChannelNumber: 6300),
            new SignalSnapshot(RsrpDbm: rsrpDbm),
            ObservationAvailability.Available,
            IsRegistered: isRegistered);
    }
}
