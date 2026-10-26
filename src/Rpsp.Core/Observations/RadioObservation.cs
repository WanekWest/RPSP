namespace Rpsp.Core.Observations;

public sealed record RadioObservation
{
    public RadioObservation(
        string observationId,
        DateTimeOffset observedAt,
        ObservationSourceKind sourceKind,
        IReadOnlyList<CellSnapshot> cellSnapshots,
        ServiceStateSnapshot? serviceState = null,
        IReadOnlyList<PhysicalChannelSnapshot>? physicalChannels = null,
        DeviceMotionSnapshot? deviceMotion = null)
    {
        ArgumentNullException.ThrowIfNull(cellSnapshots);

        ObservationId = observationId;
        ObservedAt = observedAt;
        SourceKind = sourceKind;
        CellSnapshots = cellSnapshots;
        ServiceState = serviceState;
        PhysicalChannels = physicalChannels ?? Array.Empty<PhysicalChannelSnapshot>();
        DeviceMotion = deviceMotion;
    }

    public string ObservationId { get; }

    public DateTimeOffset ObservedAt { get; }

    public ObservationSourceKind SourceKind { get; }

    public IReadOnlyList<CellSnapshot> CellSnapshots { get; }

    public ServiceStateSnapshot? ServiceState { get; }

    public IReadOnlyList<PhysicalChannelSnapshot> PhysicalChannels { get; }

    public DeviceMotionSnapshot? DeviceMotion { get; }
}
