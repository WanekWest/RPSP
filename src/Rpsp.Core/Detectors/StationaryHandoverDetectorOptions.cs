using System.Collections.Frozen;
using Rpsp.Core.Observations;

namespace Rpsp.Core.Detectors;

public sealed record StationaryHandoverDetectorOptions
{
    private static readonly FrozenSet<DeviceMotionState> DefaultStationaryMotionStates =
        new[] { DeviceMotionState.Stationary, DeviceMotionState.Walking }.ToFrozenSet();

    private static readonly FrozenSet<DeviceMotionState> DefaultBenignMotionStates =
        new[] { DeviceMotionState.Moving, DeviceMotionState.InVehicle }.ToFrozenSet();

    public int MinimumObservationCount { get; init; } = 4;

    public TimeSpan MinimumWindowDuration { get; init; } = TimeSpan.FromSeconds(30D);

    public int SuspiciousTransitionCount { get; init; } = 3;

    public int HighRiskTransitionCount { get; init; } = 5;

    public int SuspiciousDistinctServingCells { get; init; } = 3;

    public int HighRiskDistinctServingCells { get; init; } = 4;

    public double SuspiciousTransitionsPerMinute { get; init; } = 2D;

    public double HighRiskTransitionsPerMinute { get; init; } = 4D;

    public IReadOnlySet<DeviceMotionState> StationaryMotionStates { get; init; } =
        DefaultStationaryMotionStates;

    public IReadOnlySet<DeviceMotionState> BenignMotionStates { get; init; } =
        DefaultBenignMotionStates;
}
