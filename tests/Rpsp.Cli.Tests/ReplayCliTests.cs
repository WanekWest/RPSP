using Rpsp.Cli;
using Xunit;

namespace Rpsp.Cli.Tests;

public sealed class ReplayCliTests
{
    private static readonly string[] ForbiddenClaimPhrases =
    [
        "attack confirmed",
        string.Concat("IM", "SI-catcher detected"),
        string.Concat("lawful intercept ", "detection"),
        string.Concat("definitive IM", "SI-catcher identification")
    ];

    private static readonly string[] SensitiveIdentifierTerms =
    [
        string.Concat("IM", "SI"),
        string.Concat("IM", "EI"),
        string.Concat("MS", "ISDN"),
        string.Concat("sub", "scriber"),
        "device identifier",
        "hardware-id"
    ];

    [Fact]
    public void ListsReplayAndStressScenarios()
    {
        CliRunResult result = Run("list-scenarios");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("StationaryStableServingCell", result.Output, StringComparison.Ordinal);
        Assert.Contains("MixedSuspiciousBurst", result.Output, StringComparison.Ordinal);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void StableScenarioReturnsLowRiskResult()
    {
        CliRunResult result = Run("run-scenario", "StationaryStableServingCell");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("kind=StationaryStableServingCell", result.Output, StringComparison.Ordinal);
        Assert.Contains("band=low", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void SuspiciousScenarioReturnsElevatedResult()
    {
        CliRunResult result = Run("run-scenario", "StationaryHighChurn");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("kind=StationaryHighChurn", result.Output, StringComparison.Ordinal);
        Assert.Contains("band=high", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void StressTraceUsesDeclaredWindows()
    {
        CliRunResult result = Run("run-stress", "MixedSuspiciousBurst");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("source=stress", result.Output, StringComparison.Ordinal);
        Assert.Contains("window=burst", result.Output, StringComparison.Ordinal);
        Assert.Contains("band=high", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void SummaryOutputContainsNoSensitiveIdentifierTerms()
    {
        CliRunResult result = Run("run-stress", "LongMixedUrbanDayStress");

        Assert.Equal(0, result.ExitCode);

        foreach (string term in SensitiveIdentifierTerms)
        {
            Assert.DoesNotContain(term, result.Output, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SummaryOutputContainsNoForbiddenClaimPhrases()
    {
        CliRunResult result = Run("run-scenario", "StationaryHighChurn");

        Assert.Equal(0, result.ExitCode);

        foreach (string phrase in ForbiddenClaimPhrases)
        {
            Assert.DoesNotContain(phrase, result.Output, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void UnknownScenarioFailsSafely()
    {
        CliRunResult result = Run("run-scenario", "MissingScenario");

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("Unknown replay scenario", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportExportIsDeterministic()
    {
        CliRunResult first = Run("export-report", "scenario", "StationaryStableServingCell");
        CliRunResult second = Run("export-report", "scenario", "StationaryStableServingCell");

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(first.Output, second.Output);
        Assert.Contains("RPSP detector report", first.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluationExportCommandProducesMarkdownEvidenceTable()
    {
        CliRunResult result = Run("export-evaluation", "markdown");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("| Source kind | Source name | Window | Detector family |", result.Output, StringComparison.Ordinal);
        Assert.Contains("replay/synthetic", result.Output, StringComparison.Ordinal);
        Assert.Contains("CompositeRiskScorer", result.Output, StringComparison.Ordinal);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void StressSummaryExportCommandFailsSafelyForUnknownTrace()
    {
        CliRunResult result = Run("export-stress-summary", "MissingStress", "csv");

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("Unknown stress trace", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void StressSummaryExportAllProducesDeterministicJsonEvidence()
    {
        CliRunResult first = Run("export-stress-summary", "all", "json");
        CliRunResult second = Run("export-stress-summary", "all", "json");

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(first.Output, second.Output);
        Assert.Contains("\"title\": \"Synthetic stress trace evidence summary\"", first.Output, StringComparison.Ordinal);
        Assert.Contains("\"dataSource\": \"replay/synthetic\"", first.Output, StringComparison.Ordinal);
        Assert.Contains("\"sourceName\": \"MixedSuspiciousBurst\"", first.Output, StringComparison.Ordinal);
        Assert.Empty(first.Error);

        foreach (string phrase in ForbiddenClaimPhrases)
        {
            Assert.DoesNotContain(phrase, first.Output, StringComparison.OrdinalIgnoreCase);
        }

        foreach (string term in SensitiveIdentifierTerms)
        {
            Assert.DoesNotContain(term, first.Output, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ExperimentCommandsWriteAndValidateEvidencePackage()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"rpsp-cli-experiment-{Guid.NewGuid():N}");
        string manifestPath = Path.Combine(temporaryRoot, "manifest.json");
        string outputDirectory = Path.Combine(temporaryRoot, "results");

        try
        {
            Directory.CreateDirectory(temporaryRoot);
            File.WriteAllText(
                manifestPath,
                """
                {
                  "schemaVersion": "1.0",
                  "experimentId": "cli-test",
                  "replicates": 1,
                  "baseSeed": 7,
                  "boundaryScales": [1.0],
                  "maximumSignalNoiseDb": 0.0,
                  "maximumTimingJitterSeconds": 0.0,
                  "includeReplayScenarios": true,
                  "includeStressTraces": false,
                  "captureTiming": false
                }
                """);

            CliRunResult run = Run("run-experiment", manifestPath, outputDirectory);
            CliRunResult validation = Run("validate-results", manifestPath, outputDirectory);

            Assert.Equal(0, run.ExitCode);
            Assert.Contains("records=180", run.Output, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "experiment-results.json")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "checksums.sha256")));
            Assert.Equal(0, validation.ExitCode);
            Assert.Contains("validation=passed", validation.Output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static CliRunResult Run(params string[] args)
    {
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        using var error = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);

        int exitCode = ReplayCli.Run(args, output, error);

        return new CliRunResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CliRunResult(
        int ExitCode,
        string Output,
        string Error);
}
