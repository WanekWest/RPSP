using System.Collections.Frozen;
using Rpsp.Core.Observations;

namespace Rpsp.Core.Detectors;

public sealed record UnknownStrongCellDetectorOptions
{
    private static readonly FrozenSet<DeviceMotionState> DefaultStationaryMotionStates =
        new[] { DeviceMotionState.Stationary, DeviceMotionState.Walking }.ToFrozenSet();

    private static readonly FrozenSet<DeviceMotionState> DefaultBenignMotionStates =
        new[] { DeviceMotionState.Moving, DeviceMotionState.InVehicle }.ToFrozenSet();

    public int MinimumObservationCount { get; init; } = 4;

    public TimeSpan MinimumWindowDuration { get; init; } = TimeSpan.FromSeconds(30D);

    public int MinimumTrustedBaselineCellCount { get; init; } = 2;

    public double StrongRsrpThresholdDbm { get; init; } = -80D;

    public double VeryStrongRsrpThresholdDbm { get; init; } = -65D;

    public int MinimumUnknownStrongAppearances { get; init; } = 2;

    public int SuspiciousUnknownStrongServingAppearances { get; init; } = 2;

    public int HighRiskUnknownStrongServingAppearances { get; init; } = 4;

    public int SuspiciousUnknownStrongNeighborAppearances { get; init; } = 3;

    public double StrongSignalScoreWeight { get; init; } = 0.08D;

    public double ServingCellScoreWeight { get; init; } = 1D;

    public double RepeatedAppearanceScoreWeight { get; init; } = 0.08D;

    public IReadOnlySet<DeviceMotionState> StationaryMotionStates { get; init; } =
        DefaultStationaryMotionStates;

    public IReadOnlySet<DeviceMotionState> BenignMotionStates { get; init; } =
        DefaultBenignMotionStates;
}
