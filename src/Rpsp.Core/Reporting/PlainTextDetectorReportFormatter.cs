using System.Globalization;
using System.Text;

namespace Rpsp.Core.Reporting;

public sealed class PlainTextDetectorReportFormatter : IDetectorReportFormatter
{
    private const char NewLine = '\n';

    public string Format(DetectorReport report, DetectorReportFormatterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(report);

        options ??= new DetectorReportFormatterOptions();
        var builder = new StringBuilder();

        AppendLine(builder, "RPSP detector report");
        AppendLine(builder, string.Create(CultureInfo.InvariantCulture, $"Detector: {report.DetectorName}"));
        AppendLine(builder, string.Create(CultureInfo.InvariantCulture, $"Decision: {report.Decision}"));
        AppendLine(builder, string.Create(CultureInfo.InvariantCulture, $"Risk score: {report.RiskScore.Value:F3}"));
        AppendLine(builder, string.Create(CultureInfo.InvariantCulture, $"Severity: {report.RiskScore.Severity}"));
        AppendLine(builder, string.Create(CultureInfo.InvariantCulture, $"Warning level: {report.WarningLevel}"));
        AppendLine(builder, string.Create(CultureInfo.InvariantCulture, $"Observation window: {FormatWindow(report)}"));
        AppendLine(builder, string.Create(CultureInfo.InvariantCulture, $"Summary: {report.Summary}"));

        foreach (DetectorReportSection section in report.Sections)
        {
            AppendLine(builder, string.Empty);
            AppendLine(builder, string.Create(CultureInfo.InvariantCulture, $"{section.Title}:"));

            foreach (DetectorReportItem item in section.Items)
            {
                AppendLine(builder, string.Create(CultureInfo.InvariantCulture, $"- {item.Name}: {item.Value}"));
            }
        }

        if (options.IncludeTechnicalIdentifiers)
        {
            AppendLine(builder, string.Empty);
            AppendLine(builder, "Technical identifiers: omitted by current formatter policy.");
        }

        return builder.ToString().TrimEnd('\n');
    }

    private static string FormatWindow(DetectorReport report)
    {
        string start = report.WindowStart?.ToString("O", CultureInfo.InvariantCulture) ?? "unknown";
        string end = report.WindowEnd?.ToString("O", CultureInfo.InvariantCulture) ?? "unknown";

        return string.Create(CultureInfo.InvariantCulture, $"{start} to {end}");
    }

    private static void AppendLine(StringBuilder builder, string value)
    {
        builder.Append(value);
        builder.Append(NewLine);
    }
}
