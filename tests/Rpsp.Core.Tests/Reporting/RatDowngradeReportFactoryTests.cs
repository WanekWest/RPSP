using Rpsp.Core.Detectors;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Reporting;
using Rpsp.Core.Scoring;
using Xunit;

namespace Rpsp.Core.Tests.Reporting;

public sealed class RatDowngradeReportFactoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StableModernRatResultProducesNoAnomalyReport()
    {
        RatDowngradeDetectionResult result = Analyze(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte
            ]);

        DetectorReport report = RatDowngradeReportFactory.Create(result);

        Assert.Equal("No anomaly", report.Decision);
        Assert.Equal(RiskSeverity.None, report.RiskScore.Severity);
        Assert.Equal(WarningLevel.None, report.WarningLevel);
    }

    [Fact]
    public void SuspiciousDowngradeReportIncludesCautiousEvidenceSummary()
    {
        RatDowngradeDetectionResult result = Analyze(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Umts,
                RadioAccessTechnology.Umts
            ]);
        string report = Format(result);

        Assert.Contains("RatDowngradeRiskDetector", report, StringComparison.Ordinal);
        Assert.Contains("Risk score:", report, StringComparison.Ordinal);
        Assert.Contains("DowngradeTransitionCount", report, StringComparison.Ordinal);
        Assert.Contains("ModernRatHistoryCount", report, StringComparison.Ordinal);
        Assert.Contains("suspicious RAT downgrade pattern", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("radio-access anomaly", report, StringComparison.Ordinal);
        Assert.Contains("not proof of interception", report, StringComparison.Ordinal);
    }

    [Fact]
    public void HighDowngradeReportContainsHighSeverityAndWarningLevel()
    {
        RatDowngradeDetectionResult result = Analyze(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Gsm,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Gsm,
                RadioAccessTechnology.Gsm
            ]);
        string report = Format(result);

        Assert.Contains("Decision: High risk", report, StringComparison.Ordinal);
        Assert.Contains("Severity: High", report, StringComparison.Ordinal);
        Assert.Contains("Warning level: Warning", report, StringComparison.Ordinal);
    }

    [Fact]
    public void MovingDowngradeReportIncludesMotionSuppression()
    {
        RatDowngradeDetectionResult result = Analyze(
            DeviceMotionState.Moving,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Umts,
                RadioAccessTechnology.Gsm,
                RadioAccessTechnology.Gsm
            ]);
        string report = Format(result);

        Assert.Contains("Decision: Suppressed", report, StringComparison.Ordinal);
        Assert.Contains("suppressed:benign-motion-state", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportIsDeterministicAcrossRepeatedFormatting()
    {
        RatDowngradeDetectionResult result = Analyze(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Umts,
                RadioAccessTechnology.Umts
            ]);
        var formatter = new PlainTextDetectorReportFormatter();

        string first = formatter.Format(RatDowngradeReportFactory.Create(result));
        string second = formatter.Format(RatDowngradeReportFactory.Create(result));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ReportDoesNotContainForbiddenClaimPhrases()
    {
        RatDowngradeDetectionResult result = Analyze(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Umts,
                RadioAccessTechnology.Umts
            ]);
        string report = Format(result);
        string[] forbiddenPhrases =
        [
            string.Concat("attack", " confirmed"),
            string.Concat("IM", "SI", "-catcher detected"),
            string.Concat("lawful", " intercept", " detection"),
            string.Concat("definitive", " IM", "SI", "-catcher identification")
        ];

        foreach (string phrase in forbiddenPhrases)
        {
            Assert.DoesNotContain(phrase, report, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ReportDoesNotContainSubscriberOrDeviceIdentifierTerms()
    {
        RatDowngradeDetectionResult result = Analyze(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Umts,
                RadioAccessTechnology.Umts
            ]);
        string report = Format(result);
        string[] restrictedTerms =
        [
            string.Concat("IM", "SI"),
            string.Concat("IM", "EI"),
            string.Concat("MS", "IS", "DN"),
            string.Concat("SU", "PI"),
            string.Concat("SU", "CI")
        ];

        foreach (string term in restrictedTerms)
        {
            Assert.DoesNotContain(term, report, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ReportOmitsRawSyntheticCellIdentifiersByDefault()
    {
        RatDowngradeDetectionResult result = Analyze(
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Umts,
                RadioAccessTechnology.Umts
            ]);
        string report = Format(result);

        Assert.DoesNotContain("synthetic-rat-cell-0", report, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-rat-cell-3", report, StringComparison.Ordinal);
    }

    private static string Format(RatDowngradeDetectionResult result)
    {
        var formatter = new PlainTextDetectorReportFormatter();

        return formatter.Format(RatDowngradeReportFactory.Create(result));
    }

    private static RatDowngradeDetectionResult Analyze(
        DeviceMotionState motionState,
        IReadOnlyList<RadioAccessTechnology> radioAccessTechnologies)
    {
        RadioObservation[] observations = radioAccessTechnologies
            .Select((radioAccessTechnology, index) => CreateObservation(
                string.Create(null, $"obs-{index}"),
                Start.AddSeconds(30D * index),
                motionState,
                radioAccessTechnology,
                string.Create(null, $"synthetic-rat-cell-{index}")))
            .ToArray();

        return new RatDowngradeRiskDetector().Analyze(observations);
    }

    private static RadioObservation CreateObservation(
        string observationId,
        DateTimeOffset observedAt,
        DeviceMotionState motionState,
        RadioAccessTechnology radioAccessTechnology,
        string syntheticCellId)
    {
        var serving = new CellSnapshot(
            CellRole.Serving,
            radioAccessTechnology,
            new CellIdentifier(new Plmn("001", "01"), syntheticCellId, PhysicalCellId: 42),
            new SignalSnapshot(RsrpDbm: -82D),
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
}
