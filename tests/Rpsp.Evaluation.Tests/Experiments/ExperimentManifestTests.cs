using Rpsp.Evaluation.Experiments;
using Xunit;

namespace Rpsp.Evaluation.Tests.Experiments;

public sealed class ExperimentManifestTests
{
    [Fact]
    public void ConfigurationHashIsStableForEquivalentManifest()
    {
        var manifest = new ExperimentManifest
        {
            ExperimentId = "test-manifest",
            Replicates = 2,
            BoundaryScales = [0.75D, 1D, 1.25D]
        };

        string first = ExperimentManifestSerializer.ComputeConfigurationHash(manifest);
        string second = ExperimentManifestSerializer.ComputeConfigurationHash(manifest);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void ManifestRejectsExperimentWithNoSources()
    {
        var manifest = new ExperimentManifest
        {
            ExperimentId = "invalid",
            IncludeReplayScenarios = false,
            IncludeStressTraces = false
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(manifest.Validate);
        Assert.Contains("replay scenarios", exception.Message, StringComparison.Ordinal);
    }
}
