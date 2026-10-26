using Rpsp.Core.Baselines;
using Rpsp.Core.Events;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Abstractions;

public interface IAnomalyScorer
{
    ValueTask<RiskScore> ScoreAsync(
        EvidenceBundle evidence,
        BaselineProfile baseline,
        CancellationToken cancellationToken = default);
}
