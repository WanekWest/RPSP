using Rpsp.Core.Observations;

namespace Rpsp.Core.Abstractions;

public interface ITraceReplaySource
{
    IAsyncEnumerable<RadioObservation> ReplayAsync(CancellationToken cancellationToken = default);
}
