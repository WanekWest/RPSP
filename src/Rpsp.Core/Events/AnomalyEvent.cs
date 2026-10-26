namespace Rpsp.Core.Events;

public sealed record AnomalyEvent(
    string EventId,
    AnomalyType Type,
    EvidenceBundle Evidence,
    DateTimeOffset ObservedAt,
    string? Explanation = null);
