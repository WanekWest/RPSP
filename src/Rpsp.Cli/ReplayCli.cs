using System.Globalization;
using Rpsp.Core.Pipeline;
using Rpsp.Core.Reporting;
using Rpsp.Evaluation;
using Rpsp.Evaluation.Experiments;
using Rpsp.Evaluation.Scenarios;
using Rpsp.Evaluation.Stress;

namespace Rpsp.Cli;

internal static class ReplayCli
{
    private const int Success = 0;
    private const int UsageError = 2;

    public static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 0 || IsHelp(args[0]))
        {
            WriteUsage(output);
            return Success;
        }

        return args[0] switch
        {
            "list-scenarios" => ListScenarios(output),
            "run-scenario" => RunScenario(args, output, error),
            "run-stress" => RunStress(args, output, error),
            "export-report" => ExportReport(args, output, error),
            "export-evaluation" => ExportEvaluation(args, output, error),
            "export-stress-summary" => ExportStressSummary(args, output, error),
            "run-experiment" => RunExperiment(args, output, error),
            "validate-results" => ValidateResults(args, output, error),
            _ => Fail(error, $"Unknown command '{args[0]}'.")
        };
    }

    private static int ListScenarios(TextWriter output)
    {
        output.WriteLine("RPSP replay CLI harness");
        output.WriteLine("Mode: offline deterministic evaluation");
        output.WriteLine();
        output.WriteLine("Replay scenarios:");

        foreach (ReplayScenario scenario in ReplayScenarioCatalog.All.OrderBy(static scenario => scenario.Kind))
        {
            output.WriteLine(FormattableString.Invariant(
                $"- {scenario.Kind} | {scenario.Name} | observations={scenario.Trace.Frames.Count}"));
        }

        output.WriteLine();
        output.WriteLine("Stress traces:");

        foreach (SyntheticStressTrace trace in SyntheticStressTraceCatalog.All.OrderBy(static trace => trace.Kind))
        {
            output.WriteLine(FormattableString.Invariant(
                $"- {trace.Kind} | {trace.Name} | observations={trace.Trace.Frames.Count}"));
        }

        return Success;
    }

    private static int RunScenario(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count != 2)
        {
            return Fail(error, "run-scenario requires exactly one replay scenario kind.");
        }

        if (!Enum.TryParse(args[1], ignoreCase: true, out ReplayScenarioKind kind))
        {
            return Fail(error, $"Unknown replay scenario '{args[1]}'.");
        }

        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
        ReplayPipelineRunResult result = ReplayPipelineRunner.Run(
            scenario.Trace,
            new RpspDetectionPipeline(),
            scenario.Baseline,
            ReplayPipelineWindowStrategy.FullTrace);

        WriteRunSummary(output, "replay", scenario.Kind.ToString(), scenario.Name, result);
        return Success;
    }

    private static int RunStress(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count != 2)
        {
            return Fail(error, "run-stress requires exactly one stress trace kind.");
        }

        if (!Enum.TryParse(args[1], ignoreCase: true, out SyntheticStressTraceKind kind))
        {
            return Fail(error, $"Unknown stress trace '{args[1]}'.");
        }

        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(kind);
        ReplayPipelineRunResult result = ReplayPipelineRunner.Run(trace);

        WriteRunSummary(output, "stress", trace.Kind.ToString(), trace.Name, result);
        return Success;
    }

    private static int ExportReport(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count != 3)
        {
            return Fail(error, "export-report requires a source kind and a scenario or stress name.");
        }

        ReplayPipelineRunResult result;

        if (string.Equals(args[1], "scenario", StringComparison.Ordinal))
        {
            if (!Enum.TryParse(args[2], ignoreCase: true, out ReplayScenarioKind kind))
            {
                return Fail(error, $"Unknown replay scenario '{args[2]}'.");
            }

            ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
            result = ReplayPipelineRunner.Run(
                scenario.Trace,
                new RpspDetectionPipeline(),
                scenario.Baseline,
                ReplayPipelineWindowStrategy.FullTrace);
        }
        else if (string.Equals(args[1], "stress", StringComparison.Ordinal))
        {
            if (!Enum.TryParse(args[2], ignoreCase: true, out SyntheticStressTraceKind kind))
            {
                return Fail(error, $"Unknown stress trace '{args[2]}'.");
            }

            result = ReplayPipelineRunner.Run(SyntheticStressTraceCatalog.Get(kind));
        }
        else
        {
            return Fail(error, $"Unknown report source '{args[1]}'.");
        }

        var formatter = new PlainTextDetectorReportFormatter();

        foreach (ReplayPipelineWindowResult window in result.Windows)
        {
            output.WriteLine(FormattableString.Invariant(
                $"# window={window.WindowName}; start={window.StartIndex}; observations={window.ObservationCount}"));
            output.WriteLine(formatter.Format(window.CompositeReport));
        }

        return Success;
    }

    private static int ExportEvaluation(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count != 2)
        {
            return Fail(error, "export-evaluation requires one format: markdown, csv or json.");
        }

        if (!TryParseFormat(args[1], out EvaluationExportFormat format))
        {
            return Fail(error, $"Unknown evaluation export format '{args[1]}'.");
        }

        EvaluationResultTable table = EvaluationResultExporter.FromReplayScenarios(ReplayScenarioCatalog.All);
        output.Write(EvaluationResultExporter.Export(table, format));
        return Success;
    }

    private static int ExportStressSummary(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count != 3)
        {
            return Fail(error, "export-stress-summary requires a stress trace kind or 'all' and one format: markdown, csv or json.");
        }

        if (!TryParseFormat(args[2], out EvaluationExportFormat format))
        {
            return Fail(error, $"Unknown evaluation export format '{args[2]}'.");
        }

        EvaluationResultTable table;

        if (string.Equals(args[1], "all", StringComparison.OrdinalIgnoreCase))
        {
            table = EvaluationResultExporter.FromStressTraces(SyntheticStressTraceCatalog.All);
        }
        else
        {
            if (!Enum.TryParse(args[1], ignoreCase: true, out SyntheticStressTraceKind kind))
            {
                return Fail(error, $"Unknown stress trace '{args[1]}'.");
            }

            table = EvaluationResultExporter.FromStressTrace(SyntheticStressTraceCatalog.Get(kind));
        }

        output.Write(EvaluationResultExporter.Export(table, format));
        return Success;
    }

    private static int RunExperiment(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count is < 2 or > 3)
        {
            return Fail(error, "run-experiment requires a manifest path and an optional output directory.");
        }

        try
        {
            ExperimentManifest manifest = ExperimentManifestSerializer.Load(args[1]);
            string outputDirectory = args.Count == 3
                ? args[2]
                : Path.Combine("artifacts", "experiments", manifest.ExperimentId);
            string configurationHash = ExperimentManifestSerializer.ComputeConfigurationHash(manifest);
            var provenance = new RunProvenance(
                Environment.GetEnvironmentVariable("RPSP_GIT_COMMIT") ?? "unrecorded",
                Environment.GetEnvironmentVariable("RPSP_DOTNET_SDK") ?? "unrecorded",
                Environment.Version.ToString(),
                configurationHash,
                manifest.CaptureTiming ? "wall-clock" : "disabled");
            ExperimentResultSet resultSet = ExperimentRunner.Run(manifest, provenance);
            ExperimentResultExporter.WritePackage(resultSet, outputDirectory);

            output.WriteLine(FormattableString.Invariant($"experiment={manifest.ExperimentId}"));
            output.WriteLine(FormattableString.Invariant($"configuration_sha256={configurationHash}"));
            output.WriteLine(FormattableString.Invariant($"records={resultSet.Records.Count}"));
            output.WriteLine(FormattableString.Invariant($"output={Path.GetFullPath(outputDirectory)}"));
            return Success;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or System.Text.Json.JsonException
            or ArgumentException
            or InvalidOperationException)
        {
            return Fail(error, $"Experiment failed: {exception.Message}");
        }
    }

    private static int ValidateResults(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count != 3)
        {
            return Fail(error, "validate-results requires a manifest path and a result directory.");
        }

        try
        {
            ExperimentManifest manifest = ExperimentManifestSerializer.Load(args[1]);
            IReadOnlyList<string> errors = ExperimentResultExporter.ValidatePackage(manifest, args[2]);

            if (errors.Count != 0)
            {
                foreach (string validationError in errors)
                {
                    error.WriteLine(validationError);
                }

                return UsageError;
            }

            output.WriteLine("validation=passed");
            output.WriteLine(FormattableString.Invariant($"experiment={manifest.ExperimentId}"));
            return Success;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or System.Text.Json.JsonException
            or ArgumentException
            or InvalidOperationException)
        {
            return Fail(error, $"Validation failed: {exception.Message}");
        }
    }

    private static void WriteRunSummary(
        TextWriter output,
        string source,
        string kind,
        string name,
        ReplayPipelineRunResult result)
    {
        output.WriteLine("RPSP replay CLI harness");
        output.WriteLine(FormattableString.Invariant($"source={source}"));
        output.WriteLine(FormattableString.Invariant($"kind={kind}"));
        output.WriteLine(FormattableString.Invariant($"name={name}"));
        output.WriteLine(FormattableString.Invariant($"windows={result.Windows.Count}"));

        foreach (ReplayPipelineWindowResult window in result.Windows)
        {
            double score = window.CompositeAssessment.RiskScore.Value;

            output.WriteLine(
                "window={0}; start={1}; observations={2}; score={3}; severity={4}; warning={5}; band={6}",
                window.WindowName,
                window.StartIndex,
                window.ObservationCount,
                score.ToString("F3", CultureInfo.InvariantCulture),
                window.CompositeAssessment.RiskScore.Severity,
                window.CompositeAssessment.WarningDecision.Level,
                GetBand(score));
        }
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

    private static void WriteUsage(TextWriter output)
    {
        output.WriteLine("RPSP replay CLI harness");
        output.WriteLine("Usage:");
        output.WriteLine("  list-scenarios");
        output.WriteLine("  run-scenario <ReplayScenarioKind>");
        output.WriteLine("  run-stress <SyntheticStressTraceKind>");
        output.WriteLine("  export-report <scenario|stress> <kind>");
        output.WriteLine("  export-evaluation <markdown|csv|json>");
        output.WriteLine("  export-stress-summary <SyntheticStressTraceKind|all> <markdown|csv|json>");
        output.WriteLine("  run-experiment <manifest> [output-directory]");
        output.WriteLine("  validate-results <manifest> <result-directory>");
    }

    private static int Fail(TextWriter error, string message)
    {
        error.WriteLine(message);
        return UsageError;
    }

    private static bool IsHelp(string value)
    {
        return string.Equals(value, "-h", StringComparison.Ordinal)
            || string.Equals(value, "--help", StringComparison.Ordinal)
            || string.Equals(value, "help", StringComparison.Ordinal);
    }

    private static bool TryParseFormat(string value, out EvaluationExportFormat format)
    {
        return Enum.TryParse(value, ignoreCase: true, out format);
    }
}
