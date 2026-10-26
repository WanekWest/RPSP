using Rpsp.Core.Observations;

namespace Rpsp.Core.Abstractions;

public interface IObservationSource
{
    IAsyncEnumerable<RadioObservation> ObserveAsync(CancellationToken cancellationToken = default);
}
