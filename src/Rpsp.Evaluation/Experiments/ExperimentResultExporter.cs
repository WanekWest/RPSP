using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rpsp.Evaluation.Experiments;

public static class ExperimentResultExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public static string ExportJson(ExperimentResultSet resultSet)
    {
        ArgumentNullException.ThrowIfNull(resultSet);
        return JsonSerializer.Serialize(resultSet, JsonOptions) + Environment.NewLine;
    }

    public static ExperimentResultSet ImportJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<ExperimentResultSet>(json, JsonOptions)
            ?? throw new InvalidOperationException("The experiment result set is empty.");
    }

    public static string ExportCsv(ExperimentResultSet resultSet)
    {
        ArgumentNullException.ThrowIfNull(resultSet);

        var builder = new StringBuilder();
        builder.AppendLine("schema_version,experiment_id,run_id,git_commit,dotnet_sdk_version,runtime_version,configuration_hash,source_kind,source_name,window,ablation_profile,boundary_scale,replicate,seed,constructed_label,detector_family,risk_score,score_band,warning_decision,suppression_state,suppression_reason,evidence_level,data_source,observation_count,elapsed_ms,timing_mode");

        foreach (ExperimentResultRecord record in resultSet.Records)
        {
            builder.Append(Csv(record.SchemaVersion)).Append(',')
                .Append(Csv(record.ExperimentId)).Append(',')
                .Append(Csv(record.RunId)).Append(',')
                .Append(Csv(record.GitCommit)).Append(',')
                .Append(Csv(record.DotnetSdkVersion)).Append(',')
                .Append(Csv(record.RuntimeVersion)).Append(',')
                .Append(Csv(record.ConfigurationHash)).Append(',')
                .Append(Csv(record.SourceKind)).Append(',')
                .Append(Csv(record.SourceName)).Append(',')
                .Append(Csv(record.WindowName)).Append(',')
                .Append(Csv(record.AblationProfile)).Append(',')
                .Append(record.BoundaryScale.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                .Append(record.Replicate.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(record.Seed.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(record.ConstructedLabel)).Append(',')
                .Append(Csv(record.DetectorFamily)).Append(',')
                .Append(record.RiskScore.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(record.ScoreBand)).Append(',')
                .Append(Csv(record.WarningDecision)).Append(',')
                .Append(Csv(record.SuppressionState)).Append(',')
                .Append(Csv(record.SuppressionReason)).Append(',')
                .Append(Csv(record.EvidenceLevel)).Append(',')
                .Append(Csv(record.DataSource)).Append(',')
                .Append(record.ObservationCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(record.ElapsedMilliseconds.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(record.TimingMode)).AppendLine();
        }

        return builder.ToString();
    }

    public static string ExportSummaryMarkdown(ExperimentResultSet resultSet)
    {
        ArgumentNullException.ThrowIfNull(resultSet);

        var builder = new StringBuilder();
        builder.AppendLine("# Synthetic Sensitivity Experiment Summary");
        builder.AppendLine();
        builder.AppendLine("These results measure agreement with constructed replay and stress labels. They are not field accuracy estimates and do not establish attack attribution.");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Experiment: `{resultSet.Manifest.ExperimentId}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Configuration SHA-256: `{resultSet.Provenance.ConfigurationHash}`");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Records: {resultSet.Records.Count}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Replicates per scale: {resultSet.Manifest.Replicates}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"- Timing mode: `{resultSet.Provenance.TimingMode}`");
        builder.AppendLine();
        builder.AppendLine("| Profile | Detector | Scale | Labeled rows | Positive agreement | Negative agreement | Suppression rate |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---:|");

        var groups = resultSet.Records
            .GroupBy(static record => new { record.AblationProfile, record.DetectorFamily, record.BoundaryScale })
            .OrderBy(static group => group.Key.AblationProfile, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.DetectorFamily, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.BoundaryScale);

        foreach (var group in groups)
        {
            ExperimentResultRecord[] labeled = group
                .Where(static record => record.ConstructedLabel is "positive" or "negative")
                .ToArray();
            int positives = labeled.Count(static record => record.ConstructedLabel == "positive");
            int negatives = labeled.Count(static record => record.ConstructedLabel == "negative");
            int truePositives = labeled.Count(static record => record.ConstructedLabel == "positive" && record.RiskScore >= 0.5D);
            int trueNegatives = labeled.Count(static record => record.ConstructedLabel == "negative" && record.RiskScore < 0.5D);
            double positiveAgreement = positives == 0 ? double.NaN : truePositives / (double)positives;
            double negativeAgreement = negatives == 0 ? double.NaN : trueNegatives / (double)negatives;
            double suppressionRate = group.Count(static record => record.SuppressionState == "suppressed") / (double)group.Count();

            builder.Append("| ").Append(group.Key.AblationProfile)
                .Append(" | ").Append(group.Key.DetectorFamily)
                .Append(" | ").Append(group.Key.BoundaryScale.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ").Append(labeled.Length.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(FormatRate(positiveAgreement))
                .Append(" | ").Append(FormatRate(negativeAgreement))
                .Append(" | ").Append(FormatRate(suppressionRate)).AppendLine(" |");
        }

        return builder.ToString();
    }

    public static void WritePackage(ExperimentResultSet resultSet, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(resultSet);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        string[] existing = Directory.GetFileSystemEntries(outputDirectory);

        if (existing.Length != 0)
        {
            throw new InvalidOperationException("The experiment output directory must be empty.");
        }

        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["experiment-results.json"] = ExportJson(resultSet),
            ["experiment-results.csv"] = ExportCsv(resultSet),
            ["experiment-summary.md"] = ExportSummaryMarkdown(resultSet),
            ["manifest.normalized.json"] = ExperimentManifestSerializer.Serialize(resultSet.Manifest)
        };

        foreach ((string name, string content) in files)
        {
            File.WriteAllText(Path.Combine(outputDirectory, name), content, new UTF8Encoding(false));
        }

        string checksumManifest = string.Join(
            Environment.NewLine,
            files.Keys.Order(StringComparer.Ordinal).Select(name =>
                $"{ComputeFileHash(Path.Combine(outputDirectory, name))}  {name}")) + Environment.NewLine;
        File.WriteAllText(
            Path.Combine(outputDirectory, "checksums.sha256"),
            checksumManifest,
            new UTF8Encoding(false));
    }

    public static IReadOnlyList<string> ValidatePackage(
        ExperimentManifest manifest,
        string resultDirectory)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultDirectory);

        var errors = new List<string>();
        string resultsPath = Path.Combine(resultDirectory, "experiment-results.json");

        if (!File.Exists(resultsPath))
        {
            return ["experiment-results.json is missing."];
        }

        ExperimentResultSet resultSet = ImportJson(File.ReadAllText(resultsPath));
        string expectedHash = ExperimentManifestSerializer.ComputeConfigurationHash(manifest);

        if (!string.Equals(resultSet.Manifest.ExperimentId, manifest.ExperimentId, StringComparison.Ordinal))
        {
            errors.Add("Experiment identifier does not match the manifest.");
        }

        if (!string.Equals(resultSet.Provenance.ConfigurationHash, expectedHash, StringComparison.Ordinal))
        {
            errors.Add("Configuration hash does not match the manifest.");
        }

        if (resultSet.Records.Count == 0)
        {
            errors.Add("The result set contains no records.");
        }

        if (resultSet.Records.Any(static record =>
            !double.IsFinite(record.RiskScore)
            || record.RiskScore < 0D
            || record.RiskScore > 1D
            || record.ObservationCount < 0
            || string.IsNullOrWhiteSpace(record.RunId)
            || string.IsNullOrWhiteSpace(record.ConstructedLabel)))
        {
            errors.Add("At least one record has invalid required values.");
        }

        string checksumPath = Path.Combine(resultDirectory, "checksums.sha256");

        if (!File.Exists(checksumPath))
        {
            errors.Add("checksums.sha256 is missing.");
        }
        else
        {
            foreach (string line in File.ReadLines(checksumPath).Where(static value => !string.IsNullOrWhiteSpace(value)))
            {
                string[] parts = line.Split("  ", 2, StringSplitOptions.None);

                if (parts.Length != 2)
                {
                    errors.Add($"Invalid checksum line: {line}");
                    continue;
                }

                string filePath = Path.Combine(resultDirectory, parts[1]);

                if (!File.Exists(filePath) || !string.Equals(ComputeFileHash(filePath), parts[0], StringComparison.Ordinal))
                {
                    errors.Add($"Checksum mismatch: {parts[1]}");
                }
            }
        }

        return errors;
    }

    private static string FormatRate(double value)
    {
        return double.IsNaN(value) ? "n/a" : value.ToString("P1", CultureInfo.InvariantCulture);
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

    private static string ComputeFileHash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
