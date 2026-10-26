using Rpsp.Core.Baselines;

namespace Rpsp.Core.Abstractions;

public interface IBaselineStore
{
    ValueTask<BaselineProfile?> LoadAsync(string profileId, CancellationToken cancellationToken = default);

    ValueTask SaveAsync(BaselineProfile profile, CancellationToken cancellationToken = default);
}
