using Rpsp.Core.Events;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Reporting;

public sealed record DetectorReport
{
    public DetectorReport(
        string detectorName,
        string decision,
        string summary,
        RiskScore riskScore,
        WarningLevel warningLevel,
        DateTimeOffset? windowStart,
        DateTimeOffset? windowEnd,
        IReadOnlyList<DetectorReportSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        DetectorName = detectorName;
        Decision = decision;
        Summary = summary;
        RiskScore = riskScore;
        WarningLevel = warningLevel;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
        Sections = sections;
    }

    public string DetectorName { get; }

    public string Decision { get; }

    public string Summary { get; }

    public RiskScore RiskScore { get; }

    public WarningLevel WarningLevel { get; }

    public DateTimeOffset? WindowStart { get; }

    public DateTimeOffset? WindowEnd { get; }

    public IReadOnlyList<DetectorReportSection> Sections { get; }
}
