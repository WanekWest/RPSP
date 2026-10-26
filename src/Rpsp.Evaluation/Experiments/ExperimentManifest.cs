using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rpsp.Evaluation.Experiments;

public sealed record ExperimentManifest
{
    public const string CurrentSchemaVersion = "1.0";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string ExperimentId { get; init; } = string.Empty;

    public int Replicates { get; init; } = 30;

    public int BaseSeed { get; init; } = 20260905;

    public IReadOnlyList<double> BoundaryScales { get; init; } = [0.75D, 1D, 1.25D];

    public IReadOnlyList<string> AblationProfiles { get; init; } = [ExperimentAblationProfiles.Full];

    public double MaximumSignalNoiseDb { get; init; } = 1.5D;

    public double MaximumTimingJitterSeconds { get; init; } = 2D;

    public bool IncludeReplayScenarios { get; init; } = true;

    public bool IncludeStressTraces { get; init; } = true;

    public bool CaptureTiming { get; init; }

    public void Validate()
    {
        if (!string.Equals(SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported experiment manifest schema '{SchemaVersion}'. Expected '{CurrentSchemaVersion}'.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(ExperimentId);
        ArgumentOutOfRangeException.ThrowIfLessThan(Replicates, 1);

        if (BoundaryScales.Count == 0)
        {
            throw new InvalidOperationException("At least one boundary scale is required.");
        }

        if (BoundaryScales.Any(static scale => !double.IsFinite(scale) || scale <= 0D))
        {
            throw new InvalidOperationException("Boundary scales must be finite and greater than zero.");
        }

        ExperimentAblationProfiles.Validate(AblationProfiles);

        if (!double.IsFinite(MaximumSignalNoiseDb) || MaximumSignalNoiseDb < 0D)
        {
            throw new InvalidOperationException("Maximum signal noise must be finite and non-negative.");
        }

        if (!double.IsFinite(MaximumTimingJitterSeconds) || MaximumTimingJitterSeconds < 0D)
        {
            throw new InvalidOperationException("Maximum timing jitter must be finite and non-negative.");
        }

        if (!IncludeReplayScenarios && !IncludeStressTraces)
        {
            throw new InvalidOperationException("The experiment must include replay scenarios, stress traces, or both.");
        }
    }
}

public static class ExperimentManifestSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public static ExperimentManifest Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string json = File.ReadAllText(path);
        ExperimentManifest manifest = JsonSerializer.Deserialize<ExperimentManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException("The experiment manifest is empty.");
        manifest.Validate();
        return manifest;
    }

    public static string Serialize(ExperimentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        manifest.Validate();
        return JsonSerializer.Serialize(manifest, JsonOptions) + Environment.NewLine;
    }

    public static string ComputeConfigurationHash(ExperimentManifest manifest)
    {
        string normalized = Serialize(manifest).Replace(Environment.NewLine, "\n", StringComparison.Ordinal);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(digest);
    }
}
