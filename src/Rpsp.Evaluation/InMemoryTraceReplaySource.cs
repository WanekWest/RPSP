using System.Runtime.CompilerServices;
using Rpsp.Core.Abstractions;
using Rpsp.Core.Observations;

namespace Rpsp.Evaluation;

public sealed class InMemoryTraceReplaySource : ITraceReplaySource
{
    private readonly ReplayTrace _trace;

    public InMemoryTraceReplaySource(
        ReplayTrace trace,
        ReplayClockMode clockMode = ReplayClockMode.Deterministic)
    {
        ArgumentNullException.ThrowIfNull(trace);

        _trace = trace;
        ClockMode = clockMode;
    }

    public ReplayClockMode ClockMode { get; }

    public async IAsyncEnumerable<RadioObservation> ReplayAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (TraceFrame frame in _trace.Frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return frame.Observation;

            await Task.Yield();
        }
    }
}
