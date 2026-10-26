using System.Globalization;
using System.Text;
using System.Text.Json;
using Rpsp.Core.Events;
using Rpsp.Core.Pipeline;
using Rpsp.Core.Scoring;
using Rpsp.Evaluation.Scenarios;
using Rpsp.Evaluation.Stress;

namespace Rpsp.Evaluation;

public enum EvaluationExportFormat
{
    Markdown = 0,
    Csv = 1,
    Json = 2
}

public sealed record EvaluationResultRecord(
    string SourceKind,
    string SourceName,
    string WindowName,
    string DetectorFamily,
    string ScoreBand,
    string WarningDecision,
    string SuppressionState,
    string EvidenceLevel,
    string DataSource);

public sealed record EvaluationResultTable(
    string Title,
    IReadOnlyList<EvaluationResultRecord> Records);

public static class EvaluationResultExporter
{
    private const string ReplaySyntheticDataSource = "replay/synthetic";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static EvaluationResultTable FromReplayScenario(ReplayScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        ReplayPipelineRunResult result = ReplayPipelineRunner.Run(
            scenario.Trace,
            new RpspDetectionPipeline(),
            scenario.Baseline,
            ReplayPipelineWindowStrategy.FullTrace);

        return new EvaluationResultTable(
            $"Replay scenario evidence: {scenario.Kind}",
            CreateRecords("replay", scenario.Kind.ToString(), result));
    }

    public static EvaluationResultTable FromReplayScenarios(IEnumerable<ReplayScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);

        EvaluationResultRecord[] records = scenarios
            .OrderBy(static scenario => scenario.Kind)
            .SelectMany(scenario => FromReplayScenario(scenario).Records)
            .ToArray();

        return new EvaluationResultTable("Replay scenario evidence summary", records);
    }

    public static EvaluationResultTable FromStressTrace(SyntheticStressTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);

        ReplayPipelineRunResult result = ReplayPipelineRunner.Run(trace);

        return new EvaluationResultTable(
            $"Synthetic stress trace evidence: {trace.Kind}",
            CreateRecords("stress", trace.Kind.ToString(), result));
    }

    public static EvaluationResultTable FromStressTraces(IEnumerable<SyntheticStressTrace> traces)
    {
        ArgumentNullException.ThrowIfNull(traces);

        EvaluationResultRecord[] records = traces
            .OrderBy(static trace => trace.Kind)
            .SelectMany(trace => FromStressTrace(trace).Records)
            .ToArray();

        return new EvaluationResultTable("Synthetic stress trace evidence summary", records);
    }

    public static string Export(EvaluationResultTable table, EvaluationExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(table);

        return format switch
        {
            EvaluationExportFormat.Markdown => ExportMarkdown(table),
            EvaluationExportFormat.Csv => ExportCsv(table),
            EvaluationExportFormat.Json => JsonSerializer.Serialize(table, JsonOptions),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown evaluation export format.")
        };
    }

    private static EvaluationResultRecord[] CreateRecords(
        string sourceKind,
        string sourceName,
        ReplayPipelineRunResult result)
    {
        var records = new List<EvaluationResultRecord>();

        foreach (ReplayPipelineWindowResult window in result.Windows)
        {
            foreach (DetectorRiskContribution contribution in window.PipelineResult.DetectorContributions)
            {
                records.Add(new EvaluationResultRecord(
                    sourceKind,
                    sourceName,
                    window.WindowName,
                    contribution.DetectorName,
                    GetBand(contribution.RiskScore.Value),
                    contribution.WarningLevel.ToString(),
                    contribution.IsSuppressed ? "suppressed" : "active",
                    GetEvidenceLevel(contribution.Evidence.Items),
                    ReplaySyntheticDataSource));
            }

            CompositeRiskAssessment composite = window.CompositeAssessment;
            records.Add(new EvaluationResultRecord(
                sourceKind,
                sourceName,
                window.WindowName,
                "CompositeRiskScorer",
                GetBand(composite.RiskScore.Value),
                composite.WarningDecision.Level.ToString(),
                composite.IsSuppressed ? "suppressed" : "active",
                GetEvidenceLevel(composite.Evidence.Items),
                ReplaySyntheticDataSource));
        }

        return records.ToArray();
    }

    private static string ExportMarkdown(EvaluationResultTable table)
    {
        var builder = new StringBuilder();
        builder.AppendLine("| Source kind | Source name | Window | Detector family | Score band | Warning decision | Suppression state | Evidence level | Data source |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|---|");

        foreach (EvaluationResultRecord record in table.Records)
        {
            builder.Append("| ")
                .Append(EscapeMarkdown(record.SourceKind)).Append(" | ")
                .Append(EscapeMarkdown(record.SourceName)).Append(" | ")
                .Append(EscapeMarkdown(record.WindowName)).Append(" | ")
                .Append(EscapeMarkdown(record.DetectorFamily)).Append(" | ")
                .Append(EscapeMarkdown(record.ScoreBand)).Append(" | ")
                .Append(EscapeMarkdown(record.WarningDecision)).Append(" | ")
                .Append(EscapeMarkdown(record.SuppressionState)).Append(" | ")
                .Append(EscapeMarkdown(record.EvidenceLevel)).Append(" | ")
                .Append(EscapeMarkdown(record.DataSource)).AppendLine(" |");
        }

        return builder.ToString();
    }

    private static string ExportCsv(EvaluationResultTable table)
    {
        var builder = new StringBuilder();
        builder.AppendLine("source_kind,source_name,window,detector_family,score_band,warning_decision,suppression_state,evidence_level,data_source");

        foreach (EvaluationResultRecord record in table.Records)
        {
            builder.Append(Csv(record.SourceKind)).Append(',')
                .Append(Csv(record.SourceName)).Append(',')
                .Append(Csv(record.WindowName)).Append(',')
                .Append(Csv(record.DetectorFamily)).Append(',')
                .Append(Csv(record.ScoreBand)).Append(',')
                .Append(Csv(record.WarningDecision)).Append(',')
                .Append(Csv(record.SuppressionState)).Append(',')
                .Append(Csv(record.EvidenceLevel)).Append(',')
                .Append(Csv(record.DataSource)).AppendLine();
        }

        return builder.ToString();
    }

    private static string GetBand(double score)
    {
        if (score >= 0.8D)
        {
            return "high";
        }

        if (score >= 0.5D)
        {
            return "elevated";
        }

        return "low";
    }

    private static string GetEvidenceLevel(IReadOnlyList<EvidenceItem> items)
    {
        if (items.Count == 0)
        {
            return "none";
        }

        double maxWeight = items.Max(static item => item.Weight);

        if (maxWeight >= 0.8D)
        {
            return "high";
        }

        if (maxWeight >= 0.4D)
        {
            return "medium";
        }

        return "low";
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string Csv(string value)
    {
        if (!value.Contains('"', StringComparison.Ordinal)
            && !value.Contains(',', StringComparison.Ordinal)
            && !value.Contains('\n', StringComparison.Ordinal)
            && !value.Contains('\r', StringComparison.Ordinal))
        {
            return value;
        }

        return FormattableString.Invariant($"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"");
    }
}
