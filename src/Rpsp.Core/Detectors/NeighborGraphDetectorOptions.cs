using System.Collections.Frozen;
using Rpsp.Core.Observations;

namespace Rpsp.Core.Detectors;

public sealed record NeighborGraphDetectorOptions
{
    private static readonly FrozenSet<DeviceMotionState> DefaultStationaryMotionStates =
        new[] { DeviceMotionState.Stationary, DeviceMotionState.Walking }.ToFrozenSet();

    private static readonly FrozenSet<DeviceMotionState> DefaultBenignMotionStates =
        new[] { DeviceMotionState.Moving, DeviceMotionState.InVehicle }.ToFrozenSet();

    public int MinimumObservationCount { get; init; } = 4;

    public TimeSpan MinimumWindowDuration { get; init; } = TimeSpan.FromSeconds(30D);

    public int MinimumBaselineNeighborCellCount { get; init; } = 2;

    public int MinimumObservedNeighborCellCount { get; init; } = 2;

    public double SuspiciousJaccardSimilarityThreshold { get; init; } = 0.50D;

    public double HighRiskJaccardSimilarityThreshold { get; init; } = 0.25D;

    public int MinimumLowSimilarityObservations { get; init; } = 2;

    public int HighRiskLowSimilarityObservations { get; init; } = 4;

    public double ServingCellChangeBoost { get; init; } = 0.08D;

    public double RepeatedDriftBoost { get; init; } = 0.08D;

    public double MaximumCorrelationBoost { get; init; } = 0.16D;

    public IReadOnlySet<DeviceMotionState> StationaryMotionStates { get; init; } =
        DefaultStationaryMotionStates;

    public IReadOnlySet<DeviceMotionState> BenignMotionStates { get; init; } =
        DefaultBenignMotionStates;
}
