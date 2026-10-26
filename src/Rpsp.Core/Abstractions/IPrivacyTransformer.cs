using Rpsp.Core.Observations;

namespace Rpsp.Core.Abstractions;

public interface IPrivacyTransformer
{
    ValueTask<RadioObservation> TransformAsync(
        RadioObservation observation,
        CancellationToken cancellationToken = default);
}
