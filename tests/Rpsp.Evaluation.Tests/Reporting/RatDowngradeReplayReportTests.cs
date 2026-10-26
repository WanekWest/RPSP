using Rpsp.Core.Detectors;
using Rpsp.Core.Reporting;
using Rpsp.Evaluation.Scenarios;
using Xunit;

namespace Rpsp.Evaluation.Tests.Reporting;

public sealed class RatDowngradeReplayReportTests
{
    [Fact]
    public async Task RatScenarioReportsMatchExpectedDecisions()
    {
        var detector = new RatDowngradeRiskDetector();
        var formatter = new PlainTextDetectorReportFormatter();

        foreach (ReplayScenario scenario in ReplayScenarioCatalog.All.Where(static scenario => scenario.HasRatDowngradeExpectation))
        {
            var source = new InMemoryTraceReplaySource(scenario.Trace);
            RatDowngradeDetectionResult result = await detector.AnalyzeAsync(source.ReplayAsync());
            string report = formatter.Format(RatDowngradeReportFactory.Create(result));

            Assert.Contains("RatDowngradeRiskDetector", report, StringComparison.Ordinal);

            Assert.Contains(
                string.Create(null, $"Decision: {GetExpectedDecision(scenario.Kind)}"),
                report,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SuspiciousRatScenarioReportContainsExpectedEvidence()
    {
        string report = await FormatScenarioAsync(ReplayScenarioKind.SuspiciousLteToUmtsDowngrade);

        Assert.Contains("Decision: Suspicious", report, StringComparison.Ordinal);
        Assert.Contains("Risk score:", report, StringComparison.Ordinal);
        Assert.Contains("DowngradeTransitionCount", report, StringComparison.Ordinal);
        Assert.Contains("ModernRatHistoryCount", report, StringComparison.Ordinal);
        Assert.Contains("radio-access anomaly", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HighRatScenarioReportContainsHighSeverityAndWarningLevel()
    {
        string report = await FormatScenarioAsync(ReplayScenarioKind.HighRiskLteToGsmDowngrade);

        Assert.Contains("Decision: High risk", report, StringComparison.Ordinal);
        Assert.Contains("Severity: High", report, StringComparison.Ordinal);
        Assert.Contains("Warning level: Warning", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MovingRatScenarioReportIncludesMotionSuppression()
    {
        string report = await FormatScenarioAsync(ReplayScenarioKind.MovingCoverageLossBenignDowngrade);

        Assert.Contains("Decision: Suppressed", report, StringComparison.Ordinal);
        Assert.Contains("suppressed:benign-motion-state", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RatScenarioReportIsDeterministic()
    {
        string first = await FormatScenarioAsync(ReplayScenarioKind.SuspiciousLteToUmtsDowngrade);
        string second = await FormatScenarioAsync(ReplayScenarioKind.SuspiciousLteToUmtsDowngrade);

        Assert.Equal(first, second);
    }

    private static async Task<string> FormatScenarioAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
        var source = new InMemoryTraceReplaySource(scenario.Trace);
        var detector = new RatDowngradeRiskDetector();
        var formatter = new PlainTextDetectorReportFormatter();

        RatDowngradeDetectionResult result = await detector
            .AnalyzeAsync(source.ReplayAsync())
            .ConfigureAwait(false);

        return formatter.Format(RatDowngradeReportFactory.Create(result));
    }

    private static string GetExpectedDecision(ReplayScenarioKind kind)
    {
        return kind switch
        {
            ReplayScenarioKind.StableLteNoDowngrade => "No anomaly",
            ReplayScenarioKind.StableNrNoDowngrade => "No anomaly",
            ReplayScenarioKind.SuspiciousLteToUmtsDowngrade => "Suspicious",
            ReplayScenarioKind.HighRiskLteToGsmDowngrade => "High risk",
            ReplayScenarioKind.NrToLteBenignFallback => "No anomaly",
            ReplayScenarioKind.MovingCoverageLossBenignDowngrade => "Suppressed",
            ReplayScenarioKind.IndoorCoverageDegradationBenign => "Suspicious",
            ReplayScenarioKind.NoServiceThenLegacyRatSuspicious => "High risk",
            ReplayScenarioKind.ColdStartInsufficientRatHistory => "Suppressed",
            ReplayScenarioKind.UnknownRatValuesAreIgnoredOrSuppressed => "No anomaly",
            _ => "No anomaly"
        };
    }
}
