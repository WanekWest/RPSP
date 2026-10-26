namespace Rpsp.Android.Tests;

internal sealed class FakeAndroidTelemetrySource : IAndroidTelemetrySource
{
    private readonly IReadOnlyList<AndroidTelemetrySnapshot> _snapshots;

    public FakeAndroidTelemetrySource(IReadOnlyList<AndroidTelemetrySnapshot> snapshots)
    {
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
    }

    public int ReadCount { get; private set; }

    public ValueTask<IReadOnlyList<AndroidTelemetrySnapshot>> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;

        return ValueTask.FromResult(_snapshots);
    }
}
