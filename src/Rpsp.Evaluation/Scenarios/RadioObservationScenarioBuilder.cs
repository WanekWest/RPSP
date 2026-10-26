using System.Globalization;
using Rpsp.Core.Observations;

namespace Rpsp.Evaluation.Scenarios;

public sealed class RadioObservationScenarioBuilder
{
    public static readonly DateTimeOffset BaseTimestamp =
        new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly ReplayScenarioKind _kind;
    private readonly List<TraceFrame> _frames = [];

    public RadioObservationScenarioBuilder(ReplayScenarioKind kind)
    {
        _kind = kind;
    }

    public RadioObservationScenarioBuilder AddServingCell(
        TimeSpan offset,
        DeviceMotionState motionState,
        string syntheticCellId,
        double rsrpDbm = -82D,
        double rsrqDb = -9D,
        double sinrDb = 18D,
        RadioAccessTechnology radioAccessTechnology = RadioAccessTechnology.Lte,
        bool isInService = true)
    {
        DateTimeOffset observedAt = BaseTimestamp.Add(offset);
        var serving = new CellSnapshot(
            CellRole.Serving,
            radioAccessTechnology,
            new CellIdentifier(
                new Plmn("001", "01"),
                syntheticCellId,
                PhysicalCellId: CalculatePhysicalCellId(syntheticCellId),
                TrackingAreaCode: 100,
                ChannelNumber: 6300),
            new SignalSnapshot(RsrpDbm: rsrpDbm, RsrqDb: rsrqDb, SinrDb: sinrDb),
            ObservationAvailability.Available,
            IsRegistered: true);

        AddObservation(
            observedAt,
            motionState,
            [serving],
            new ServiceStateSnapshot(
                IsInService: isInService,
                VoiceRadioAccessTechnology: radioAccessTechnology,
                DataRadioAccessTechnology: radioAccessTechnology));

        return this;
    }

    public RadioObservationScenarioBuilder AddObservationWithoutServingCell(
        TimeSpan offset,
        DeviceMotionState motionState,
        string syntheticNeighborCellId,
        double? rsrpDbm = -88D,
        double? rsrqDb = -10D,
        double? sinrDb = 12D,
        RadioAccessTechnology radioAccessTechnology = RadioAccessTechnology.Lte)
    {
        DateTimeOffset observedAt = BaseTimestamp.Add(offset);
        var neighbor = new CellSnapshot(
            CellRole.Neighbor,
            radioAccessTechnology,
            new CellIdentifier(
                new Plmn("001", "01"),
                syntheticNeighborCellId,
                PhysicalCellId: CalculatePhysicalCellId(syntheticNeighborCellId),
                TrackingAreaCode: 100,
                ChannelNumber: 6300),
            new SignalSnapshot(RsrpDbm: rsrpDbm, RsrqDb: rsrqDb, SinrDb: sinrDb),
            ObservationAvailability.Available,
            IsRegistered: false);

        AddObservation(
            observedAt,
            motionState,
            [neighbor],
            new ServiceStateSnapshot(
                IsInService: true,
                VoiceRadioAccessTechnology: radioAccessTechnology,
                DataRadioAccessTechnology: radioAccessTechnology));

        return this;
    }

    public RadioObservationScenarioBuilder AddServingCellWithNeighbor(
        TimeSpan offset,
        DeviceMotionState motionState,
        string servingSyntheticCellId,
        string neighborSyntheticCellId,
        double servingRsrpDbm = -82D,
        double neighborRsrpDbm = -78D,
        double servingRsrqDb = -9D,
        double neighborRsrqDb = -8D,
        double servingSinrDb = 18D,
        double neighborSinrDb = 20D,
        RadioAccessTechnology radioAccessTechnology = RadioAccessTechnology.Lte)
    {
        DateTimeOffset observedAt = BaseTimestamp.Add(offset);
        var serving = CreateCell(
            CellRole.Serving,
            radioAccessTechnology,
            servingSyntheticCellId,
            new SignalSnapshot(RsrpDbm: servingRsrpDbm, RsrqDb: servingRsrqDb, SinrDb: servingSinrDb),
            isRegistered: true);
        var neighbor = CreateCell(
            CellRole.Neighbor,
            radioAccessTechnology,
            neighborSyntheticCellId,
            new SignalSnapshot(RsrpDbm: neighborRsrpDbm, RsrqDb: neighborRsrqDb, SinrDb: neighborSinrDb),
            isRegistered: false);

        AddObservation(
            observedAt,
            motionState,
            [serving, neighbor],
            new ServiceStateSnapshot(
                IsInService: true,
                VoiceRadioAccessTechnology: radioAccessTechnology,
                DataRadioAccessTechnology: radioAccessTechnology));

        return this;
    }

    public RadioObservationScenarioBuilder AddServingCellWithNeighbors(
        TimeSpan offset,
        DeviceMotionState motionState,
        string servingSyntheticCellId,
        IReadOnlyList<string> neighborSyntheticCellIds,
        double servingRsrpDbm = -82D,
        double neighborRsrpDbm = -86D,
        RadioAccessTechnology radioAccessTechnology = RadioAccessTechnology.Lte)
    {
        ArgumentNullException.ThrowIfNull(neighborSyntheticCellIds);

        DateTimeOffset observedAt = BaseTimestamp.Add(offset);
        var cells = new List<CellSnapshot>
        {
            CreateCell(
                CellRole.Serving,
                radioAccessTechnology,
                servingSyntheticCellId,
                new SignalSnapshot(RsrpDbm: servingRsrpDbm, RsrqDb: -9D, SinrDb: 18D),
                isRegistered: true)
        };

        foreach (string neighborSyntheticCellId in neighborSyntheticCellIds)
        {
            cells.Add(CreateCell(
                CellRole.Neighbor,
                radioAccessTechnology,
                neighborSyntheticCellId,
                new SignalSnapshot(RsrpDbm: neighborRsrpDbm, RsrqDb: -10D, SinrDb: 14D),
                isRegistered: false));
        }

        AddObservation(
            observedAt,
            motionState,
            cells,
            new ServiceStateSnapshot(
                IsInService: true,
                VoiceRadioAccessTechnology: radioAccessTechnology,
                DataRadioAccessTechnology: radioAccessTechnology));

        return this;
    }

    public RadioObservationScenarioBuilder AddServingCellWithUnavailableSignal(
        TimeSpan offset,
        DeviceMotionState motionState,
        string syntheticCellId,
        RadioAccessTechnology radioAccessTechnology = RadioAccessTechnology.Lte)
    {
        DateTimeOffset observedAt = BaseTimestamp.Add(offset);
        var serving = CreateCell(
            CellRole.Serving,
            radioAccessTechnology,
            syntheticCellId,
            new SignalSnapshot(),
            isRegistered: true);

        AddObservation(
            observedAt,
            motionState,
            [serving],
            new ServiceStateSnapshot(
                IsInService: true,
                VoiceRadioAccessTechnology: radioAccessTechnology,
                DataRadioAccessTechnology: radioAccessTechnology));

        return this;
    }

    public RadioObservationScenarioBuilder AddServiceUnavailable(
        TimeSpan offset,
        DeviceMotionState motionState)
    {
        DateTimeOffset observedAt = BaseTimestamp.Add(offset);

        AddObservation(
            observedAt,
            motionState,
            [],
            new ServiceStateSnapshot(
                IsInService: false,
                VoiceRadioAccessTechnology: RadioAccessTechnology.Unknown,
                DataRadioAccessTechnology: RadioAccessTechnology.Unknown));

        return this;
    }

    public RadioObservationScenarioBuilder AddUnknownRatObservation(
        TimeSpan offset,
        DeviceMotionState motionState)
    {
        DateTimeOffset observedAt = BaseTimestamp.Add(offset);

        AddObservation(
            observedAt,
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
        DateTimeOffset observedAt,
        DeviceMotionState motionState,
        IReadOnlyList<CellSnapshot> cells,
        ServiceStateSnapshot? serviceState)
    {
        string observationId = string.Create(
            CultureInfo.InvariantCulture,
            $"{_kind}-Observation-{_frames.Count}");
        var deviceMotion = new DeviceMotionSnapshot(motionState, confidence: 0.95D);
        var observation = new RadioObservation(
            observationId,
            observedAt,
            ObservationSourceKind.Replay,
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
                TrackingAreaCode: 100,
                ChannelNumber: 6300),
            signal,
            ObservationAvailability.Available,
            IsRegistered: isRegistered);
    }

    private static int CalculatePhysicalCellId(string syntheticCellId)
    {
        int hash = syntheticCellId.Aggregate(
            0,
            static (current, character) => current + character);

        return 1 + (hash % 503);
    }
}
