using Rpsp.Core.Scoring;

namespace Rpsp.Core.Events;

public sealed record WarningDecision(
    string DecisionId,
    WarningLevel Level,
    RiskScore RiskScore,
    AnomalyEvent? Event = null)
{
    public RiskSeverity Severity => RiskScore.Severity;
}
