using Rpsp.Core.Detectors;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;
using Xunit;

namespace Rpsp.Core.Tests.Detectors;

public sealed class RatDowngradeRiskDetectorTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StableLteDoesNotTriggerAnomaly()
    {
        var detector = new RatDowngradeRiskDetector();
        RadioObservation[] observations = CreateTrace(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte
            ],
            TimeSpan.FromSeconds(30D));

        RatDowngradeDetectionResult result = detector.Analyze(observations);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:no-legacy-downgrade", result.SuppressionReason);
        Assert.Equal(RiskSeverity.None, result.RiskScore.Severity);
        Assert.Equal(WarningLevel.None, result.WarningDecision.Level);
        Assert.True(result.RiskScore.Value <= 0.1D);
    }

    [Fact]
    public void StableNrDoesNotTriggerAnomaly()
    {
        var detector = new RatDowngradeRiskDetector();
        RadioObservation[] observations = CreateTrace(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Nr
            ],
            TimeSpan.FromSeconds(30D));

        RatDowngradeDetectionResult result = detector.Analyze(observations);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:no-legacy-downgrade", result.SuppressionReason);
        Assert.Equal(RiskSeverity.None, result.RiskScore.Severity);
    }

    [Fact]
    public void StationaryLteToUmtsTriggersSuspiciousDowngrade()
    {
        var detector = new RatDowngradeRiskDetector();
        RadioObservation[] observations = CreateTrace(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Umts,
                RadioAccessTechnology.Umts
            ],
            TimeSpan.FromSeconds(30D));

        RatDowngradeDetectionResult result = detector.Analyze(observations);

        Assert.NotNull(result.Event);
        Assert.Equal(AnomalyType.SuspiciousDowngrade, result.Event.Type);
        Assert.True(result.RiskScore.Value >= 0.6D);
        Assert.Equal(RiskSeverity.Medium, result.RiskScore.Severity);
        Assert.Equal(WarningLevel.Caution, result.WarningDecision.Level);
        AssertEvidenceContains(result, "DowngradeTransitionCount", "1");
        AssertEvidenceContains(result, "ModernRatHistoryCount", "3");
    }

    [Fact]
    public void StationaryLteToGsmRepeatedDowngradeProducesHighSeverity()
    {
        var detector = new RatDowngradeRiskDetector();
        RadioObservation[] observations = CreateTrace(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Gsm,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Gsm,
                RadioAccessTechnology.Gsm
            ],
            TimeSpan.FromSeconds(30D));

        RatDowngradeDetectionResult result = detector.Analyze(observations);

        Assert.NotNull(result.Event);
        Assert.True(result.RiskScore.Value >= 0.8D);
        Assert.Equal(RiskSeverity.High, result.RiskScore.Severity);
        Assert.Equal(WarningLevel.Warning, result.WarningDecision.Level);
        AssertEvidenceContains(result, "DowngradeTransitionCount", "2");
    }

    [Fact]
    public void NrToLteFallbackIsNotHighRisk()
    {
        var detector = new RatDowngradeRiskDetector();
        RadioObservation[] observations = CreateTrace(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte
            ],
            TimeSpan.FromSeconds(30D));

        RatDowngradeDetectionResult result = detector.Analyze(observations);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:no-legacy-downgrade", result.SuppressionReason);
        Assert.True(result.RiskScore.Value <= 0.1D);
        Assert.NotEqual(RiskSeverity.High, result.RiskScore.Severity);
    }

    [Theory]
    [InlineData(DeviceMotionState.Moving)]
    [InlineData(DeviceMotionState.InVehicle)]
    public void MovingCoverageLossDowngradeIsSuppressed(DeviceMotionState motionState)
    {
        var detector = new RatDowngradeRiskDetector();
        RadioObservation[] observations = CreateTrace(
            motionState,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Umts,
                RadioAccessTechnology.Gsm,
                RadioAccessTechnology.Gsm
            ],
            TimeSpan.FromSeconds(30D));

        RatDowngradeDetectionResult result = detector.Analyze(observations);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:benign-motion-state", result.SuppressionReason);
        Assert.True(result.RiskScore.Value <= 0.1D);
        AssertEvidenceContains(result, "SuppressionReason", "benign-motion-state");
    }

    [Fact]
    public void IndoorDegradationBenignScenarioIsNotHighRisk()
    {
        var detector = new RatDowngradeRiskDetector();
        RadioObservation[] observations =
        [
            CreateObservation("obs-0", Start, DeviceMotionState.Stationary, RadioAccessTechnology.Lte, -80D),
            CreateObservation("obs-1", Start.AddSeconds(20D), DeviceMotionState.Stationary, RadioAccessTechnology.Lte, -101D),
            CreateObservation("obs-2", Start.AddSeconds(40D), DeviceMotionState.Stationary, RadioAccessTechnology.Umts, -106D),
            CreateObservation("obs-3", Start.AddSeconds(60D), DeviceMotionState.Stationary, RadioAccessTechnology.Lte, -94D),
            CreateObservation("obs-4", Start.AddSeconds(80D), DeviceMotionState.Stationary, RadioAccessTechnology.Lte, -84D)
        ];

        RatDowngradeDetectionResult result = detector.Analyze(observations);

        Assert.True(result.RiskScore.Value < 0.8D);
        Assert.NotEqual(RiskSeverity.High, result.RiskScore.Severity);
    }

    [Fact]
    public void ServiceUnavailableThenLegacyRatAfterModernHistoryProducesEvidence()
    {
        var detector = new RatDowngradeRiskDetector();
        RadioObservation[] observations =
        [
            CreateObservation("obs-0", Start, DeviceMotionState.Stationary, RadioAccessTechnology.Lte),
            CreateObservation("obs-1", Start.AddSeconds(30D), DeviceMotionState.Stationary, RadioAccessTechnology.Lte),
            CreateServiceUnavailableObservation("obs-2", Start.AddSeconds(60D), DeviceMotionState.Stationary),
            CreateObservation("obs-3", Start.AddSeconds(90D), DeviceMotionState.Stationary, RadioAccessTechnology.Umts),
            CreateObservation("obs-4", Start.AddSeconds(120D), DeviceMotionState.Stationary, RadioAccessTechnology.Umts)
        ];

        RatDowngradeDetectionResult result = detector.Analyze(observations);

        Assert.NotNull(result.Event);
        Assert.True(result.RiskScore.Value >= 0.6D);
        AssertEvidenceContains(result, "NoServiceGapDetected", "True");
        AssertEvidenceContains(result, "ServiceFlapCount", "2");
    }

    [Fact]
    public void ColdStartIsSuppressed()
    {
        var detector = new RatDowngradeRiskDetector();
        RadioObservation[] observations = CreateTrace(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Umts,
                RadioAccessTechnology.Lte
            ],
            TimeSpan.FromSeconds(30D));

        RatDowngradeDetectionResult result = detector.Analyze(observations);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:cold-start-observation-count", result.SuppressionReason);
        Assert.True(result.RiskScore.Value <= 0.1D);
    }

    [Fact]
    public void UnknownRatValuesAreSuppressed()
    {
        var detector = new RatDowngradeRiskDetector();
        RadioObservation[] observations = Enumerable.Range(0, 5)
            .Select(index => CreateUnknownRatObservation(
                string.Create(null, $"obs-{index}"),
                Start.AddSeconds(30D * index),
                DeviceMotionState.Stationary))
            .ToArray();

        RatDowngradeDetectionResult result = detector.Analyze(observations);

        Assert.Null(result.Event);
        Assert.True(result.IsSuppressed);
        Assert.Equal("suppressed:unknown-rat-history", result.SuppressionReason);
        Assert.True(result.RiskScore.Value <= 0.1D);
    }

    [Fact]
    public void RiskScoreIsAlwaysClampedToValidRange()
    {
        var detector = new RatDowngradeRiskDetector(
            new RatDowngradeDetectorOptions
            {
                SuspiciousDowngradeCount = 1,
                HighRiskDowngradeCount = 1,
                SuspiciousLegacyDwellDuration = TimeSpan.FromSeconds(1D),
                HighRiskLegacyDwellDuration = TimeSpan.FromSeconds(1D),
                SuspiciousDowngradesPerMinute = 0.1D,
                HighRiskDowngradesPerMinute = 0.1D
            });
        RadioObservation[] observations = CreateTrace(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Gsm,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Gsm,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Gsm
            ],
            TimeSpan.FromSeconds(10D));

        RatDowngradeDetectionResult result = detector.Analyze(observations);

        Assert.InRange(result.RiskScore.Value, 0D, 1D);
        Assert.True(result.RiskScore.Value <= 1D);
    }

    private static void AssertEvidenceContains(
        RatDowngradeDetectionResult result,
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
        IReadOnlyList<RadioAccessTechnology> radioAccessTechnologies,
        TimeSpan step)
    {
        return radioAccessTechnologies
            .Select((radioAccessTechnology, index) => CreateObservation(
                string.Create(null, $"obs-{index}"),
                Start.AddTicks(step.Ticks * index),
                motionState,
                radioAccessTechnology))
            .ToArray();
    }

    private static RadioObservation CreateObservation(
        string observationId,
        DateTimeOffset observedAt,
        DeviceMotionState motionState,
        RadioAccessTechnology radioAccessTechnology,
        double rsrpDbm = -82D)
    {
        var serving = new CellSnapshot(
            CellRole.Serving,
            radioAccessTechnology,
            new CellIdentifier(new Plmn("001", "01"), string.Create(null, $"synthetic-cell-{observationId}"), PhysicalCellId: 42),
            new SignalSnapshot(RsrpDbm: rsrpDbm),
            ObservationAvailability.Available,
            IsRegistered: true);
        var serviceState = new ServiceStateSnapshot(
            IsInService: true,
            VoiceRadioAccessTechnology: radioAccessTechnology,
            DataRadioAccessTechnology: radioAccessTechnology);
        var deviceMotion = new DeviceMotionSnapshot(motionState, confidence: 0.95D);

        return new RadioObservation(
            observationId,
            observedAt,
            ObservationSourceKind.Replay,
            [serving],
            serviceState,
            deviceMotion: deviceMotion);
    }

    private static RadioObservation CreateServiceUnavailableObservation(
        string observationId,
        DateTimeOffset observedAt,
        DeviceMotionState motionState)
    {
        var serviceState = new ServiceStateSnapshot(
            IsInService: false,
            VoiceRadioAccessTechnology: RadioAccessTechnology.Unknown,
            DataRadioAccessTechnology: RadioAccessTechnology.Unknown);
        var deviceMotion = new DeviceMotionSnapshot(motionState, confidence: 0.95D);

        return new RadioObservation(
            observationId,
            observedAt,
            ObservationSourceKind.Replay,
            [],
            serviceState,
            deviceMotion: deviceMotion);
    }

    private static RadioObservation CreateUnknownRatObservation(
        string observationId,
        DateTimeOffset observedAt,
        DeviceMotionState motionState)
    {
        var serviceState = new ServiceStateSnapshot(
            IsInService: true,
            VoiceRadioAccessTechnology: RadioAccessTechnology.Unknown,
            DataRadioAccessTechnology: RadioAccessTechnology.Unknown);
        var deviceMotion = new DeviceMotionSnapshot(motionState, confidence: 0.95D);

        return new RadioObservation(
            observationId,
            observedAt,
            ObservationSourceKind.Replay,
            [],
            serviceState,
            deviceMotion: deviceMotion);
    }
}
