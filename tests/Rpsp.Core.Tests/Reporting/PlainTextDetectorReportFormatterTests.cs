using Rpsp.Core.Detectors;
using Rpsp.Core.Reporting;
using Xunit;

namespace Rpsp.Core.Tests.Reporting;

public sealed class PlainTextDetectorReportFormatterTests
{
    [Fact]
    public void SuspiciousChurnReportIncludesCautiousEvidenceSummary()
    {
        StationaryHandoverDetectionResult result = StationaryHandoverReportTestData.CreateSuspiciousChurnResult();
        string report = Format(result);

        Assert.Contains("StationaryHandoverAnomalyDetector", report, StringComparison.Ordinal);
        Assert.Contains("Risk score:", report, StringComparison.Ordinal);
        Assert.Contains("ServingCellTransitionCount", report, StringComparison.Ordinal);
        Assert.Contains("DistinctServingCellCount", report, StringComparison.Ordinal);
        Assert.Contains("radio-access anomaly", report, StringComparison.Ordinal);
        Assert.Contains("compatible with suspicious reselection/handover behavior", report, StringComparison.Ordinal);
        Assert.Contains("not an attribution or proof of interception", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportIsDeterministicAcrossRepeatedFormatting()
    {
        StationaryHandoverDetectionResult result = StationaryHandoverReportTestData.CreateSuspiciousChurnResult();
        var formatter = new PlainTextDetectorReportFormatter();

        string first = formatter.Format(StationaryHandoverReportFactory.Create(result));
        string second = formatter.Format(StationaryHandoverReportFactory.Create(result));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ReportDoesNotContainForbiddenClaimPhrases()
    {
        StationaryHandoverDetectionResult result = StationaryHandoverReportTestData.CreateSuspiciousChurnResult();
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
        StationaryHandoverDetectionResult result = StationaryHandoverReportTestData.CreateSuspiciousChurnResult();
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
        StationaryHandoverDetectionResult result = StationaryHandoverReportTestData.CreateSuspiciousChurnResult();
        string report = Format(result);

        Assert.DoesNotContain("synthetic-cell-a", report, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-cell-b", report, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-cell-c", report, StringComparison.Ordinal);
    }

    private static string Format(StationaryHandoverDetectionResult result)
    {
        var formatter = new PlainTextDetectorReportFormatter();

        return formatter.Format(StationaryHandoverReportFactory.Create(result));
    }
}
