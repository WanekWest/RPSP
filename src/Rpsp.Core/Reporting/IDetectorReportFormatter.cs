namespace Rpsp.Core.Reporting;

public interface IDetectorReportFormatter
{
    string Format(DetectorReport report, DetectorReportFormatterOptions? options = null);
}
