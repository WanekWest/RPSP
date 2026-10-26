namespace Rpsp.Core.Observations;

public enum ObservationAvailability
{
    Unknown = 0,
    Available = 1,
    PartiallyAvailable = 2,
    Unavailable = 3,
    PrivilegedOnly = 4,
    Derived = 5
}
