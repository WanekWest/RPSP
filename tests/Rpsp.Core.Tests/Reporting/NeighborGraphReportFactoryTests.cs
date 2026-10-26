using Rpsp.Core.Baselines;
using Rpsp.Core.Detectors;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Reporting;
using Rpsp.Core.Scoring;
using Xunit;

namespace Rpsp.Core.Tests.Reporting;

public sealed class NeighborGraphReportFactoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StableKnownGraphProducesNoAnomalyReport()
    {
        NeighborGraphDetectionResult result = Analyze(
            ["neighbor-a", "neighbor-b", "neighbor-c"],
            DeviceMotionState.Stationary,
            [
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"])
            ]);

        DetectorReport report = NeighborGraphReportFactory.Create(result);

        Assert.Equal("No anomaly", report.Decision);
        Assert.Equal(RiskSeverity.None, report.RiskScore.Severity);
        Assert.Equal(WarningLevel.None, report.WarningLevel);
    }

    [Fact]
    public void SuspiciousNeighborGraphReportIncludesCautiousEvidenceSummary()
    {
        string report = CreateSuspiciousReport();

        Assert.Contains("NeighborGraphAnomalyDetector", report, StringComparison.Ordinal);
        Assert.Contains("Risk score:", report, StringComparison.Ordinal);
        Assert.Contains("AverageJaccardSimilarity", report, StringComparison.Ordinal);
        Assert.Contains("MinimumJaccardSimilarity", report, StringComparison.Ordinal);
        Assert.Contains("neighbor graph anomaly", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("radio-access anomaly", report, StringComparison.Ordinal);
        Assert.Contains("not proof of attack", report, StringComparison.Ordinal);
        Assert.Contains("not attribution", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportIsDeterministicAcrossRepeatedFormatting()
    {
        NeighborGraphDetectionResult result = CreateSuspiciousResult();
        var formatter = new PlainTextDetectorReportFormatter();

        string first = formatter.Format(NeighborGraphReportFactory.Create(result));
        string second = formatter.Format(NeighborGraphReportFactory.Create(result));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ReportOmitsRawCellIdentifiersByDefault()
    {
        string report = CreateSuspiciousReport();

        Assert.DoesNotContain("neighbor-x", report, StringComparison.Ordinal);
        Assert.DoesNotContain("serving-a", report, StringComparison.Ordinal);
        Assert.DoesNotContain("neighbor-a", report, StringComparison.Ordinal);
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
        return new PlainTextDetectorReportFormatter().Format(NeighborGraphReportFactory.Create(CreateSuspiciousResult()));
    }

    private static NeighborGraphDetectionResult CreateSuspiciousResult()
    {
        return Analyze(
            ["neighbor-a", "neighbor-b", "neighbor-c"],
            DeviceMotionState.Stationary,
            [
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-x", "neighbor-y", "neighbor-z"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"]),
                ("serving-a", ["neighbor-a", "neighbor-b", "neighbor-c"])
            ]);
    }

    private static NeighborGraphDetectionResult Analyze(
        string[] baselineNeighborIds,
        DeviceMotionState motionState,
        IReadOnlyList<(string ServingCellId, string[] NeighborCellIds)> frames)
    {
        BaselineProfile baseline = CreateBaseline(baselineNeighborIds);
        RadioObservation[] observations = frames
            .Select((frame, index) => CreateObservation(
                string.Create(null, $"obs-{index}"),
                Start.AddSeconds(30D * index),
                motionState,
                frame.ServingCellId,
                frame.NeighborCellIds))
            .ToArray();

        return new NeighborGraphAnomalyDetector().Analyze(observations, baseline);
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
