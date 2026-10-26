using Rpsp.Core.Events;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Detectors;

public sealed record NeighborGraphDetectionResult(
    EvidenceBundle Evidence,
    RiskScore RiskScore,
    WarningDecision WarningDecision,
    AnomalyEvent? Event,
    bool IsSuppressed,
    string? SuppressionReason);
