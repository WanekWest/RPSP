namespace Rpsp.Core.Events;

public enum AnomalyType
{
    Unknown = 0,
    NewStrongUnknownCell = 1,
    SuspiciousDowngrade = 2,
    StationaryHandover = 3,
    SignalQualityCollapse = 4,
    IdentityExposureContext = 5,
    PrivilegedOnlyIndicator = 6,
    NeighborGraphAnomaly = 7
}
