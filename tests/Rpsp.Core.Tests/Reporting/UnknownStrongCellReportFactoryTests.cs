using Rpsp.Core.Baselines;
using Rpsp.Core.Detectors;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Reporting;
using Rpsp.Core.Scoring;
using Xunit;

namespace Rpsp.Core.Tests.Reporting;

public sealed class UnknownStrongCellReportFactoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void KnownTrustedCellsProduceNoAnomalyReport()
    {
        UnknownStrongCellDetectionResult result = Analyze(
            DeviceMotionState.Stationary,
            ["synthetic-known-a", "synthetic-known-a", "synthetic-known-a", "synthetic-known-a", "synthetic-known-a"],
            [-82D, -82D, -83D, -82D, -81D],
            CreateBaseline("synthetic-known-a", "synthetic-known-b"));

        DetectorReport report = UnknownStrongCellReportFactory.Create(result);

        Assert.Equal("No anomaly", report.Decision);
        Assert.Equal(RiskSeverity.None, report.RiskScore.Severity);
        Assert.Equal(WarningLevel.None, report.WarningLevel);
    }

    [Fact]
    public void SuspiciousUnknownStrongCellReportIncludesCautiousWording()
    {
        UnknownStrongCellDetectionResult result = Analyze(
            DeviceMotionState.Stationary,
            ["synthetic-known-a", "synthetic-unknown-strong", "synthetic-unknown-strong", "synthetic-unknown-strong", "synthetic-known-a"],
            [-83D, -74D, -73D, -72D, -82D],
            CreateBaseline("synthetic-known-a", "synthetic-known-b"));
        string report = Format(result);

        Assert.Contains("UnknownStrongCellDetector", report, StringComparison.Ordinal);
        Assert.Contains("Risk score:", report, StringComparison.Ordinal);
        Assert.Contains("UnknownStrongServingAppearances", report, StringComparison.Ordinal);
        Assert.Contains("TrustedBaselineCellCount", report, StringComparison.Ordinal);
        Assert.Contains("unknown strong cell observation", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("radio-access anomaly", report, StringComparison.Ordinal);
        Assert.Contains("not proof of attack", report, StringComparison.Ordinal);
        Assert.Contains("not attribution", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportIsDeterministicAcrossRepeatedFormatting()
    {
        UnknownStrongCellDetectionResult result = Analyze(
            DeviceMotionState.Stationary,
            ["synthetic-known-a", "synthetic-unknown-strong", "synthetic-unknown-strong", "synthetic-unknown-strong", "synthetic-known-a"],
            [-83D, -74D, -73D, -72D, -82D],
            CreateBaseline("synthetic-known-a", "synthetic-known-b"));
        var formatter = new PlainTextDetectorReportFormatter();

        string first = formatter.Format(UnknownStrongCellReportFactory.Create(result));
        string second = formatter.Format(UnknownStrongCellReportFactory.Create(result));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ReportOmitsRawCellIdentifiersByDefault()
    {
        UnknownStrongCellDetectionResult result = Analyze(
            DeviceMotionState.Stationary,
            ["synthetic-known-a", "synthetic-raw-check", "synthetic-raw-check", "synthetic-raw-check", "synthetic-known-a"],
            [-83D, -74D, -73D, -72D, -82D],
            CreateBaseline("synthetic-known-a", "synthetic-known-b"));
        string report = Format(result);

        Assert.DoesNotContain("synthetic-raw-check", report, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-known-a", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportDoesNotContainForbiddenClaimPhrases()
    {
        string report = CreateSuspiciousReport();
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
    public void ReportDoesNotContainRestrictedIdentifierTerms()
    {
        string report = CreateSuspiciousReport();
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

    private static string CreateSuspiciousReport()
    {
        UnknownStrongCellDetectionResult result = Analyze(
            DeviceMotionState.Stationary,
            ["synthetic-known-a", "synthetic-unknown-strong", "synthetic-unknown-strong", "synthetic-unknown-strong", "synthetic-known-a"],
            [-83D, -74D, -73D, -72D, -82D],
            CreateBaseline("synthetic-known-a", "synthetic-known-b"));

        return Format(result);
    }

    private static string Format(UnknownStrongCellDetectionResult result)
    {
        var formatter = new PlainTextDetectorReportFormatter();

        return formatter.Format(UnknownStrongCellReportFactory.Create(result));
    }

    private static UnknownStrongCellDetectionResult Analyze(
        DeviceMotionState motionState,
        IReadOnlyList<string> cellIds,
        IReadOnlyList<double?> rsrpValues,
        BaselineProfile baseline)
    {
        RadioObservation[] observations = cellIds
            .Select((cellId, index) => CreateObservation(
                string.Create(null, $"obs-{index}"),
                Start.AddSeconds(30D * index),
                motionState,
                cellId,
                rsrpValues[index]))
            .ToArray();

        return new UnknownStrongCellDetector().Analyze(observations, baseline);
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

    private static RadioObservation CreateObservation(
        string observationId,
        DateTimeOffset observedAt,
        DeviceMotionState motionState,
        string syntheticCellId,
        double? rsrpDbm)
    {
        var serving = new CellSnapshot(
            CellRole.Serving,
            RadioAccessTechnology.Lte,
            new CellIdentifier(new Plmn("001", "01"), syntheticCellId, ChannelNumber: 6300),
            new SignalSnapshot(RsrpDbm: rsrpDbm),
            ObservationAvailability.Available,
            IsRegistered: true);
        var serviceState = new ServiceStateSnapshot(
            IsInService: true,
            VoiceRadioAccessTechnology: RadioAccessTechnology.Lte,
            DataRadioAccessTechnology: RadioAccessTechnology.Lte);
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
