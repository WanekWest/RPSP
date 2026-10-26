using Rpsp.Core.Observations;

namespace Rpsp.Core.Events;

public sealed record EvidenceItem(
    string Name,
    string Description,
    ObservationAvailability Availability,
    double Weight);
