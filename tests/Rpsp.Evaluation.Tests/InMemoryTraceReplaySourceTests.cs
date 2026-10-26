using Rpsp.Core.Observations;
using Rpsp.Evaluation;
using Xunit;

namespace Rpsp.Evaluation.Tests;

public sealed class InMemoryTraceReplaySourceTests
{
    [Fact]
    public async Task ReplayReturnsObservationsInChronologicalOrder()
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        var later = CreateObservation("later", start.AddMinutes(5D));
        var earlier = CreateObservation("earlier", start);
        var trace = new ReplayTrace(
        [
            new TraceFrame(later.ObservedAt, later),
            new TraceFrame(earlier.ObservedAt, earlier)
        ]);
        var source = new InMemoryTraceReplaySource(trace);

        IReadOnlyList<RadioObservation> observations = await CollectAsync(source.ReplayAsync());

        Assert.Collection(
            observations,
            first => Assert.Equal("earlier", first.ObservationId),
            second => Assert.Equal("later", second.ObservationId));
    }

    [Fact]
    public void ReplayAssemblyDoesNotReferencePlatformRadioPackages()
    {
        string disallowedPlatformName = string.Concat("And", "roid");

        Assert.DoesNotContain(
            typeof(InMemoryTraceReplaySource).Assembly.GetReferencedAssemblies(),
            reference => reference.Name?.Contains(disallowedPlatformName, StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task EmptyReplayCompletesWithoutObservations()
    {
        var source = new InMemoryTraceReplaySource(new ReplayTrace([]));

        IReadOnlyList<RadioObservation> observations = await CollectAsync(source.ReplayAsync());

        Assert.Empty(observations);
    }

    [Fact]
    public async Task ReplayObservesCancellationToken()
    {
        var observation = CreateObservation("obs-1", DateTimeOffset.UtcNow);
        var source = new InMemoryTraceReplaySource(
            new ReplayTrace([new TraceFrame(observation.ObservedAt, observation)]));
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync().ConfigureAwait(true);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (RadioObservation _ in source.ReplayAsync(cancellation.Token).ConfigureAwait(true))
            {
            }
        }).ConfigureAwait(true);
    }

    private static async Task<IReadOnlyList<RadioObservation>> CollectAsync(
        IAsyncEnumerable<RadioObservation> observations)
    {
        var collected = new List<RadioObservation>();

        await foreach (RadioObservation observation in observations.ConfigureAwait(false))
        {
            collected.Add(observation);
        }

        return collected;
    }

    private static RadioObservation CreateObservation(string id, DateTimeOffset observedAt)
    {
        var serving = new CellSnapshot(
            CellRole.Serving,
            RadioAccessTechnology.Lte,
            new CellIdentifier(new Plmn("001", "01"), $"cell-{id}"),
            new SignalSnapshot(RsrpDbm: -80D),
            ObservationAvailability.Available,
            IsRegistered: true);

        return new RadioObservation(
            id,
            observedAt,
            ObservationSourceKind.Replay,
            [serving]);
    }
}
