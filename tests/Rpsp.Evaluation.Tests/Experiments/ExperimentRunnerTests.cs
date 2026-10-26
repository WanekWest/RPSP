using Rpsp.Evaluation.Experiments;
using Xunit;

namespace Rpsp.Evaluation.Tests.Experiments;

public sealed class ExperimentRunnerTests
{
    [Fact]
    public void ReplayOnlyRunProducesProvenanceRichDeterministicRows()
    {
        var manifest = new ExperimentManifest
        {
            ExperimentId = "replay-only-test",
            Replicates = 1,
            BaseSeed = 100,
            BoundaryScales = [1D],
            MaximumSignalNoiseDb = 0D,
            MaximumTimingJitterSeconds = 0D,
            IncludeStressTraces = false
        };
        string hash = ExperimentManifestSerializer.ComputeConfigurationHash(manifest);
        var provenance = new RunProvenance("test-commit", "10.0.400", "10.0.0", hash, "disabled");

        ExperimentResultSet first = ExperimentRunner.Run(manifest, provenance);
        ExperimentResultSet second = ExperimentRunner.Run(manifest, provenance);

        Assert.Equal(180, first.Records.Count);
        Assert.Equal(first.Records, second.Records);
        Assert.All(first.Records, record =>
        {
            Assert.Equal(hash, record.ConfigurationHash);
            Assert.Equal("test-commit", record.GitCommit);
            Assert.Equal(0D, record.ElapsedMilliseconds);
            Assert.Equal("replay/synthetic-perturbation", record.DataSource);
        });
        Assert.Contains(first.Records, static record => record.ConstructedLabel == "positive");
        Assert.Contains(first.Records, static record => record.ConstructedLabel == "negative");
        Assert.Contains(first.Records, static record => record.ConstructedLabel == "suppression-expected");
    }

    [Fact]
    public void PackageRoundTripValidatesAndDetectsTampering()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"rpsp-experiment-{Guid.NewGuid():N}");

        try
        {
            var manifest = new ExperimentManifest
            {
                ExperimentId = "package-test",
                Replicates = 1,
                BoundaryScales = [1D],
                IncludeStressTraces = false
            };
            string hash = ExperimentManifestSerializer.ComputeConfigurationHash(manifest);
            var provenance = new RunProvenance("test-commit", "10.0.400", "10.0.0", hash, "disabled");
            ExperimentResultSet resultSet = ExperimentRunner.Run(manifest, provenance);

            ExperimentResultExporter.WritePackage(resultSet, temporaryDirectory);

            Assert.Empty(ExperimentResultExporter.ValidatePackage(manifest, temporaryDirectory));

            File.AppendAllText(Path.Combine(temporaryDirectory, "experiment-results.csv"), "tampered");
            IReadOnlyList<string> errors = ExperimentResultExporter.ValidatePackage(manifest, temporaryDirectory);
            Assert.Contains(errors, static error => error.Contains("Checksum mismatch", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void SingleDetectorAblationFiltersContributionsWithoutChangingProductionDefaults()
    {
        var manifest = new ExperimentManifest
        {
            ExperimentId = "ablation-test",
            Replicates = 1,
            BoundaryScales = [1D],
            AblationProfiles = [ExperimentAblationProfiles.Full, ExperimentAblationProfiles.StationaryOnly],
            MaximumSignalNoiseDb = 0D,
            MaximumTimingJitterSeconds = 0D,
            IncludeStressTraces = false
        };
        string hash = ExperimentManifestSerializer.ComputeConfigurationHash(manifest);
        var provenance = new RunProvenance("test-commit", "10.0.400", "10.0.0", hash, "disabled");

        ExperimentResultSet resultSet = ExperimentRunner.Run(manifest, provenance);
        ExperimentResultRecord[] stationaryOnly = resultSet.Records
            .Where(static record => record.AblationProfile == ExperimentAblationProfiles.StationaryOnly)
            .ToArray();

        Assert.Equal(72, stationaryOnly.Length);
        Assert.All(stationaryOnly, static record =>
            Assert.True(record.DetectorFamily is "StationaryHandoverAnomalyDetector" or "CompositeRiskScorer"));
        Assert.Equal(180, resultSet.Records.Count(static record => record.AblationProfile == ExperimentAblationProfiles.Full));
    }
}
