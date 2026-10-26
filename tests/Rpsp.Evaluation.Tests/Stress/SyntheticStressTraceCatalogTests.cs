using Rpsp.Core.Observations;
using Rpsp.Evaluation.Stress;
using Xunit;

namespace Rpsp.Evaluation.Tests.Stress;

public sealed class SyntheticStressTraceCatalogTests
{
    [Fact]
    public void CatalogContainsAllStressTraces()
    {
        SyntheticStressTraceKind[] expectedKinds = Enum.GetValues<SyntheticStressTraceKind>();

        Assert.Equal(expectedKinds.Length, SyntheticStressTraceCatalog.All.Count);

        foreach (SyntheticStressTraceKind kind in expectedKinds)
        {
            SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(kind);

            Assert.Equal(kind, trace.Kind);
            Assert.NotEmpty(trace.Trace.Frames);
            Assert.NotNull(trace.Baseline);
            Assert.InRange(trace.Trace.Frames.Count, 1, 48);
        }
    }

    [Fact]
    public void StressTraceGenerationIsDeterministic()
    {
        foreach (SyntheticStressTraceKind kind in Enum.GetValues<SyntheticStressTraceKind>())
        {
            string first = CreateSignature(SyntheticStressTraceCatalog.Get(kind));
            string second = CreateSignature(SyntheticStressTraceCatalog.Get(kind));

            Assert.Equal(first, second);
        }
    }

    [Fact]
    public async Task ReplayNormalizationReturnsChronologicalTimestamps()
    {
        foreach (SyntheticStressTrace trace in SyntheticStressTraceCatalog.All)
        {
            IReadOnlyList<RadioObservation> observations = await StressTraceTestSupport
                .CollectAsync(trace);

            Assert.Equal(observations.OrderBy(static observation => observation.ObservedAt), observations);
        }
    }

    [Fact]
    public void StressTracesUseOnlySyntheticNonSensitiveIdentifiers()
    {
        string[] disallowedTerms = ["subscriber", "hardware-id", "account-id", "route-id"];

        foreach (SyntheticStressTrace trace in SyntheticStressTraceCatalog.All)
        {
            IEnumerable<string> identifiers = trace.Trace.Frames
                .SelectMany(static frame => frame.Observation.CellSnapshots)
                .Select(static cell => cell.Identifier?.NetworkCellId)
                .Where(static identifier => !string.IsNullOrWhiteSpace(identifier))
                .Cast<string>();

            foreach (string identifier in identifiers)
            {
                Assert.StartsWith("synthetic-", identifier, StringComparison.Ordinal);

                foreach (string disallowedTerm in disallowedTerms)
                {
                    Assert.DoesNotContain(disallowedTerm, identifier, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    private static string CreateSignature(SyntheticStressTrace trace)
    {
        return string.Join(
            '|',
            trace.Trace.Frames.Select(static frame =>
            {
                string cells = string.Join(
                    ',',
                    frame.Observation.CellSnapshots.Select(static cell =>
                        string.Join(
                            ':',
                            cell.Role,
                            cell.RadioAccessTechnology,
                            cell.Identifier?.NetworkCellId ?? "none",
                            cell.Signal?.RsrpDbm?.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) ?? "na")));

                return $"{frame.Timestamp:O}:{cells}";
            }));
    }
}
