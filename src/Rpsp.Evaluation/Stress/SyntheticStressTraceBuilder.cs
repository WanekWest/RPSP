using System.Globalization;
using Rpsp.Core.Observations;

namespace Rpsp.Evaluation.Stress;

public sealed class SyntheticStressTraceBuilder
{
    public static readonly DateTimeOffset BaseTimestamp =
        new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SyntheticStressTraceKind _kind;
    private readonly List<TraceFrame> _frames = [];

    public SyntheticStressTraceBuilder(SyntheticStressTraceKind kind)
    {
        _kind = kind;
    }

    public SyntheticStressTraceBuilder AddServingCellWithNeighbors(
        TimeSpan offset,
        DeviceMotionState motionState,
        string servingSyntheticCellId,
        IReadOnlyList<string> neighborSyntheticCellIds,
        double? servingRsrpDbm = -82D,
        double? neighborRsrpDbm = -86D,
        RadioAccessTechnology radioAccessTechnology = RadioAccessTechnology.Lte,
        bool isInService = true)
    {
        ArgumentNullException.ThrowIfNull(neighborSyntheticCellIds);

        var cells = new List<CellSnapshot>
        {
            CreateCell(
                CellRole.Serving,
                radioAccessTechnology,
                servingSyntheticCellId,
                CreateSignal(servingRsrpDbm),
                isRegistered: true)
        };

        foreach (string neighborSyntheticCellId in neighborSyntheticCellIds)
        {
            cells.Add(CreateCell(
                CellRole.Neighbor,
                radioAccessTechnology,
                neighborSyntheticCellId,
                CreateSignal(neighborRsrpDbm),
                isRegistered: false));
        }

        AddObservation(offset, motionState, cells, CreateServiceState(radioAccessTechnology, isInService));

        return this;
    }

    public SyntheticStressTraceBuilder AddServingCell(
        TimeSpan offset,
        DeviceMotionState motionState,
        string servingSyntheticCellId,
        double? servingRsrpDbm = -82D,
        RadioAccessTechnology radioAccessTechnology = RadioAccessTechnology.Lte,
        bool isInService = true)
    {
        return AddServingCellWithNeighbors(
            offset,
            motionState,
            servingSyntheticCellId,
            [],
            servingRsrpDbm,
            neighborRsrpDbm: null,
            radioAccessTechnology,
            isInService);
    }

    public SyntheticStressTraceBuilder AddNeighborOnly(
        TimeSpan offset,
        DeviceMotionState motionState,
        IReadOnlyList<string> neighborSyntheticCellIds,
        double? neighborRsrpDbm = -92D,
        RadioAccessTechnology radioAccessTechnology = RadioAccessTechnology.Lte)
    {
        ArgumentNullException.ThrowIfNull(neighborSyntheticCellIds);

        CellSnapshot[] cells = neighborSyntheticCellIds
            .Select(neighborSyntheticCellId => CreateCell(
                CellRole.Neighbor,
                radioAccessTechnology,
                neighborSyntheticCellId,
                CreateSignal(neighborRsrpDbm),
                isRegistered: false))
            .ToArray();

        AddObservation(offset, motionState, cells, CreateServiceState(radioAccessTechnology, isInService: true));

        return this;
    }

    public SyntheticStressTraceBuilder AddServiceUnavailable(
        TimeSpan offset,
        DeviceMotionState motionState)
    {
        AddObservation(
            offset,
            motionState,
            [],
            new ServiceStateSnapshot(
                IsInService: false,
                VoiceRadioAccessTechnology: RadioAccessTechnology.Unknown,
                DataRadioAccessTechnology: RadioAccessTechnology.Unknown));

        return this;
    }

    public SyntheticStressTraceBuilder AddUnknownRatObservation(
        TimeSpan offset,
        DeviceMotionState motionState)
    {
        AddObservation(
            offset,
            motionState,
            [],
            new ServiceStateSnapshot(
                IsInService: true,
                VoiceRadioAccessTechnology: RadioAccessTechnology.Unknown,
                DataRadioAccessTechnology: RadioAccessTechnology.Unknown));

        return this;
    }

    public ReplayTrace Build()
    {
        return new ReplayTrace(_frames);
    }

    private void AddObservation(
        TimeSpan offset,
        DeviceMotionState motionState,
        IReadOnlyList<CellSnapshot> cells,
        ServiceStateSnapshot? serviceState)
    {
        DateTimeOffset observedAt = BaseTimestamp.Add(offset);
        string observationId = string.Create(
            CultureInfo.InvariantCulture,
            $"{_kind}-Observation-{_frames.Count}");
        var deviceMotion = new DeviceMotionSnapshot(motionState, confidence: 0.95D);
        var observation = new RadioObservation(
            observationId,
            observedAt,
            ObservationSourceKind.Synthetic,
            cells,
            serviceState,
            deviceMotion: deviceMotion);

        _frames.Add(new TraceFrame(observedAt, observation));
    }

    private static CellSnapshot CreateCell(
        CellRole role,
        RadioAccessTechnology radioAccessTechnology,
        string syntheticCellId,
        SignalSnapshot signal,
        bool isRegistered)
    {
        return new CellSnapshot(
            role,
            radioAccessTechnology,
            new CellIdentifier(
                new Plmn("001", "01"),
                syntheticCellId,
                PhysicalCellId: CalculatePhysicalCellId(syntheticCellId),
                TrackingAreaCode: 200,
                ChannelNumber: 6400),
            signal,
            signal.RsrpDbm is null ? ObservationAvailability.PartiallyAvailable : ObservationAvailability.Available,
            IsRegistered: isRegistered);
    }

    private static SignalSnapshot CreateSignal(double? rsrpDbm)
    {
        return new SignalSnapshot(
            RsrpDbm: rsrpDbm,
            RsrqDb: rsrpDbm is null ? null : -9D,
            SinrDb: rsrpDbm is null ? null : 18D);
    }

    private static ServiceStateSnapshot CreateServiceState(
        RadioAccessTechnology radioAccessTechnology,
        bool isInService)
    {
        return new ServiceStateSnapshot(
            IsInService: isInService,
            VoiceRadioAccessTechnology: radioAccessTechnology,
            DataRadioAccessTechnology: radioAccessTechnology);
    }

    private static int CalculatePhysicalCellId(string syntheticCellId)
    {
        int hash = syntheticCellId.Aggregate(
            0,
            static (current, character) => current + character);

        return 1 + (hash % 503);
    }
}
