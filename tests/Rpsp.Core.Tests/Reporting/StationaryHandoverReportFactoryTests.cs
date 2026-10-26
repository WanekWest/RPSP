using Rpsp.Core.Detectors;
using Rpsp.Core.Events;
using Rpsp.Core.Reporting;
using Rpsp.Core.Scoring;
using Xunit;

namespace Rpsp.Core.Tests.Reporting;

public sealed class StationaryHandoverReportFactoryTests
{
    [Fact]
    public void StableStationaryResultProducesNoAnomalyReport()
    {
        StationaryHandoverDetectionResult result = StationaryHandoverReportTestData.CreateStableStationaryResult();
        DetectorReport report = StationaryHandoverReportFactory.Create(result);

        Assert.Equal("No anomaly", report.Decision);
        Assert.Equal(RiskSeverity.None, report.RiskScore.Severity);
        Assert.Equal(WarningLevel.None, report.WarningLevel);
    }

    [Fact]
    public void HighChurnResultProducesHighRiskReport()
    {
        StationaryHandoverDetectionResult result = StationaryHandoverReportTestData.CreateHighChurnResult();
        DetectorReport report = StationaryHandoverReportFactory.Create(result);

        Assert.Equal("High risk", report.Decision);
        Assert.Equal(RiskSeverity.High, report.RiskScore.Severity);
        Assert.Equal(WarningLevel.Warning, report.WarningLevel);
    }

    [Fact]
    public void MovingBenignResultProducesSuppressedReport()
    {
        StationaryHandoverDetectionResult result = StationaryHandoverReportTestData.CreateMovingBenignResult();
        DetectorReport report = StationaryHandoverReportFactory.Create(result);

        Assert.Equal("Suppressed", report.Decision);
        Assert.Contains("suppressed:benign-motion-state", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void IndoorFadingResultDoesNotClaimHandoverAnomaly()
    {
        StationaryHandoverDetectionResult result = StationaryHandoverReportTestData.CreateIndoorFadingResult();
        DetectorReport report = StationaryHandoverReportFactory.Create(result);
        string text = new PlainTextDetectorReportFormatter().Format(report);

        Assert.Equal("No anomaly", report.Decision);
        Assert.Contains("suppressed:no-serving-cell-churn", text, StringComparison.Ordinal);
        Assert.DoesNotContain("compatible with suspicious reselection/handover behavior", text, StringComparison.Ordinal);
    }
}
