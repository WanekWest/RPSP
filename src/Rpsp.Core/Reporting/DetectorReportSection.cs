namespace Rpsp.Core.Reporting;

public sealed record DetectorReportSection
{
    public DetectorReportSection(string title, IReadOnlyList<DetectorReportItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        Title = title;
        Items = items;
    }

    public string Title { get; }

    public IReadOnlyList<DetectorReportItem> Items { get; }
}
