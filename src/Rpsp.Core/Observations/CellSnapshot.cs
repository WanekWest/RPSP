namespace Rpsp.Core.Observations;

public sealed record CellSnapshot(
    CellRole Role,
    RadioAccessTechnology RadioAccessTechnology,
    CellIdentifier? Identifier = null,
    SignalSnapshot? Signal = null,
    ObservationAvailability Availability = ObservationAvailability.Unknown,
    bool? IsRegistered = null,
    DateTimeOffset? FirstSeenAt = null,
    DateTimeOffset? LastSeenAt = null);
