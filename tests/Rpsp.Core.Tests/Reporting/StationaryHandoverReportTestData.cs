using Rpsp.Core.Detectors;
using Rpsp.Core.Observations;

namespace Rpsp.Core.Tests.Reporting;

internal static class StationaryHandoverReportTestData
{
    private static readonly DateTimeOffset Start = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    public static StationaryHandoverDetectionResult CreateStableStationaryResult()
    {
        return Analyze(
            DeviceMotionState.Stationary,
            ["synthetic-cell-a", "synthetic-cell-a", "synthetic-cell-a", "synthetic-cell-a", "synthetic-cell-a"],
            TimeSpan.FromSeconds(30D));
    }

    public static StationaryHandoverDetectionResult CreateSuspiciousChurnResult()
    {
        return Analyze(
            DeviceMotionState.Stationary,
            ["synthetic-cell-a", "synthetic-cell-b", "synthetic-cell-a", "synthetic-cell-c", "synthetic-cell-b"],
            TimeSpan.FromSeconds(30D));
    }

    public static StationaryHandoverDetectionResult CreateHighChurnResult()
    {
        return Analyze(
            DeviceMotionState.Stationary,
            [
                "synthetic-cell-a",
                "synthetic-cell-b",
                "synthetic-cell-c",
                "synthetic-cell-d",
                "synthetic-cell-a",
                "synthetic-cell-b",
                "synthetic-cell-c"
            ],
            TimeSpan.FromSeconds(20D));
    }

    public static StationaryHandoverDetectionResult CreateMovingBenignResult()
    {
        return Analyze(
            DeviceMotionState.Moving,
            ["synthetic-cell-a", "synthetic-cell-b", "synthetic-cell-c", "synthetic-cell-d", "synthetic-cell-e"],
            TimeSpan.FromSeconds(20D));
    }

    public static StationaryHandoverDetectionResult CreateIndoorFadingResult()
    {
        RadioObservation[] observations =
        [
            CreateObservation("obs-0", Start, DeviceMotionState.Stationary, "synthetic-cell-a", -80D),
            CreateObservation("obs-1", Start.AddSeconds(30D), DeviceMotionState.Stationary, "synthetic-cell-a", -96D),
            CreateObservation("obs-2", Start.AddSeconds(60D), DeviceMotionState.Stationary, "synthetic-cell-a", -110D),
            CreateObservation("obs-3", Start.AddSeconds(90D), DeviceMotionState.Stationary, "synthetic-cell-a", -91D),
            CreateObservation("obs-4", Start.AddSeconds(120D), DeviceMotionState.Stationary, "synthetic-cell-a", -82D)
        ];

        return new StationaryHandoverAnomalyDetector().Analyze(observations);
    }

    private static StationaryHandoverDetectionResult Analyze(
        DeviceMotionState motionState,
        IReadOnlyList<string> cellIds,
        TimeSpan step)
    {
        RadioObservation[] observations = cellIds
            .Select((cellId, index) => CreateObservation(
                string.Create(null, $"obs-{index}"),
                Start.AddTicks(step.Ticks * index),
                motionState,
                cellId,
                -82D))
            .ToArray();

        return new StationaryHandoverAnomalyDetector().Analyze(observations);
    }

    private static RadioObservation CreateObservation(
        string observationId,
        DateTimeOffset observedAt,
        DeviceMotionState motionState,
        string cellId,
        double rsrpDbm)
    {
        var serving = new CellSnapshot(
            CellRole.Serving,
            RadioAccessTechnology.Lte,
            new CellIdentifier(new Plmn("001", "01"), cellId, PhysicalCellId: 42),
            new SignalSnapshot(RsrpDbm: rsrpDbm),
            ObservationAvailability.Available,
            IsRegistered: true);
        var motion = new DeviceMotionSnapshot(motionState, confidence: 0.95D);

        return new RadioObservation(
            observationId,
            observedAt,
            ObservationSourceKind.Replay,
            [serving],
            deviceMotion: motion);
    }
}
