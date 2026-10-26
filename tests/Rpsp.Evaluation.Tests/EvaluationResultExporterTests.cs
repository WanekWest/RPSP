using Rpsp.Evaluation.Scenarios;
using Rpsp.Evaluation.Stress;
using Xunit;

namespace Rpsp.Evaluation.Tests;

public sealed class EvaluationResultExporterTests
{
    private static readonly string[] SensitiveTerms =
    [
        string.Concat("IM", "SI"),
        string.Concat("IM", "EI"),
        string.Concat("MS", "ISDN"),
        string.Concat("sub", "scriber"),
        "device identifier"
    ];

    [Fact]
    public void ReplayScenarioExportContainsDeterministicReplaySyntheticRows()
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(ReplayScenarioKind.StationaryStableServingCell);

        EvaluationResultTable first = EvaluationResultExporter.FromReplayScenario(scenario);
        EvaluationResultTable second = EvaluationResultExporter.FromReplayScenario(scenario);

        Assert.Equal(first.Records, second.Records);
        Assert.Equal(
            EvaluationResultExporter.Export(first, EvaluationExportFormat.Csv),
            EvaluationResultExporter.Export(second, EvaluationExportFormat.Csv));
        Assert.Contains(first.Records, record =>
            record.SourceKind == "replay"
            && record.SourceName == "StationaryStableServingCell"
            && record.DetectorFamily == "CompositeRiskScorer"
            && record.ScoreBand == "low"
            && record.DataSource == "replay/synthetic");
    }

    [Fact]
    public void MarkdownAndCsvExportsUseStableHeadersAndNoSensitiveTerms()
    {
        EvaluationResultTable table = EvaluationResultExporter.FromStressTrace(
            SyntheticStressTraceCatalog.Get(SyntheticStressTraceKind.MixedSuspiciousBurst));

        string markdown = EvaluationResultExporter.Export(table, EvaluationExportFormat.Markdown);
        string csv = EvaluationResultExporter.Export(table, EvaluationExportFormat.Csv);

        Assert.StartsWith("| Source kind | Source name | Window | Detector family | Score band | Warning decision | Suppression state | Evidence level | Data source |", markdown, StringComparison.Ordinal);
        Assert.StartsWith("source_kind,source_name,window,detector_family,score_band,warning_decision,suppression_state,evidence_level,data_source", csv, StringComparison.Ordinal);
        Assert.Contains("stress", csv, StringComparison.Ordinal);
        Assert.Contains("replay/synthetic", markdown, StringComparison.Ordinal);

        foreach (string term in SensitiveTerms)
        {
            Assert.DoesNotContain(term, markdown, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(term, csv, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void JsonExportIsStableAndLabelsReplaySyntheticDataSource()
    {
        EvaluationResultTable table = EvaluationResultExporter.FromReplayScenario(
            ReplayScenarioCatalog.Get(ReplayScenarioKind.StationaryHighChurn));

        string first = EvaluationResultExporter.Export(table, EvaluationExportFormat.Json);
        string second = EvaluationResultExporter.Export(table, EvaluationExportFormat.Json);

        Assert.Equal(first, second);
        Assert.Contains("\"dataSource\": \"replay/synthetic\"", first, StringComparison.Ordinal);
        Assert.DoesNotContain("attack confirmed", first, StringComparison.OrdinalIgnoreCase);
    }
}
