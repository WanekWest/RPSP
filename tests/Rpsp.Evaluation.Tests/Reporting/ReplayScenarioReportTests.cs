using Rpsp.Core.Detectors;
using Rpsp.Core.Reporting;
using Rpsp.Evaluation.Scenarios;
using Xunit;

namespace Rpsp.Evaluation.Tests.Reporting;

public sealed class ReplayScenarioReportTests
{
    [Fact]
    public async Task ScenarioReportsMatchExpectedDecisions()
    {
        var detector = new StationaryHandoverAnomalyDetector();
        var formatter = new PlainTextDetectorReportFormatter();

        foreach (ReplayScenario scenario in ReplayScenarioCatalog.All.Where(static scenario => scenario.HasStationaryHandoverExpectation))
        {
            var source = new InMemoryTraceReplaySource(scenario.Trace);
            StationaryHandoverDetectionResult result = await detector.AnalyzeAsync(source.ReplayAsync());
            string report = formatter.Format(StationaryHandoverReportFactory.Create(result));

            Assert.Contains("StationaryHandoverAnomalyDetector", report, StringComparison.Ordinal);

            Assert.Contains(
                string.Create(null, $"Decision: {GetExpectedDecision(scenario.Kind)}"),
                report,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SuspiciousScenarioReportContainsExpectedEvidence()
    {
        string report = await FormatScenarioAsync(ReplayScenarioKind.StationarySuspiciousChurn);

        Assert.Contains("Decision: Suspicious", report, StringComparison.Ordinal);
        Assert.Contains("Risk score:", report, StringComparison.Ordinal);
        Assert.Contains("ServingCellTransitionCount", report, StringComparison.Ordinal);
        Assert.Contains("DistinctServingCellCount", report, StringComparison.Ordinal);
        Assert.Contains("radio-access anomaly", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HighChurnScenarioReportContainsHighSeverityAndWarningLevel()
    {
        string report = await FormatScenarioAsync(ReplayScenarioKind.StationaryHighChurn);

        Assert.Contains("Decision: High risk", report, StringComparison.Ordinal);
        Assert.Contains("Severity: High", report, StringComparison.Ordinal);
        Assert.Contains("Warning level: Warning", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MovingScenarioReportIncludesMotionSuppression()
    {
        string report = await FormatScenarioAsync(ReplayScenarioKind.MovingUrbanBenignHandover);

        Assert.Contains("Decision: Suppressed", report, StringComparison.Ordinal);
        Assert.Contains("suppressed:benign-motion-state", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IndoorFadingScenarioReportDoesNotClaimHandoverAnomaly()
    {
        string report = await FormatScenarioAsync(ReplayScenarioKind.IndoorFadingWithoutCellChurn);

        Assert.Contains("Decision: No anomaly", report, StringComparison.Ordinal);
        Assert.Contains("suppressed:no-serving-cell-churn", report, StringComparison.Ordinal);
        Assert.DoesNotContain("compatible with suspicious reselection/handover behavior", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScenarioReportIsDeterministic()
    {
        string first = await FormatScenarioAsync(ReplayScenarioKind.StationarySuspiciousChurn);
        string second = await FormatScenarioAsync(ReplayScenarioKind.StationarySuspiciousChurn);

        Assert.Equal(first, second);
    }

    private static async Task<string> FormatScenarioAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
        var source = new InMemoryTraceReplaySource(scenario.Trace);
        var detector = new StationaryHandoverAnomalyDetector();
        var formatter = new PlainTextDetectorReportFormatter();

        StationaryHandoverDetectionResult result = await detector
            .AnalyzeAsync(source.ReplayAsync())
            .ConfigureAwait(false);

        return formatter.Format(StationaryHandoverReportFactory.Create(result));
    }

    private static string GetExpectedDecision(ReplayScenarioKind kind)
    {
        return kind switch
        {
            ReplayScenarioKind.StationaryStableServingCell => "No anomaly",
            ReplayScenarioKind.StationarySuspiciousChurn => "Suspicious",
            ReplayScenarioKind.StationaryHighChurn => "High risk",
            ReplayScenarioKind.MovingUrbanBenignHandover => "Suppressed",
            ReplayScenarioKind.InVehicleFastBenignHandover => "Suppressed",
            ReplayScenarioKind.IndoorFadingWithoutCellChurn => "No anomaly",
            ReplayScenarioKind.BorderCellOscillationLowRisk => "No anomaly",
            ReplayScenarioKind.ColdStartInsufficientObservations => "Suppressed",
            ReplayScenarioKind.MissingServingCells => "Suppressed",
            ReplayScenarioKind.OutOfOrderInputNormalizedByReplay => "Suspicious",
            _ => "No anomaly"
        };
    }
}
