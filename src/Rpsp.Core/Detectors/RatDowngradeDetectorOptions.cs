using System.Collections.Frozen;
using Rpsp.Core.Observations;

namespace Rpsp.Core.Detectors;

public sealed record RatDowngradeDetectorOptions
{
    private static readonly FrozenSet<RadioAccessTechnology> DefaultModernRadioAccessTechnologies =
        new[] { RadioAccessTechnology.Nr, RadioAccessTechnology.Lte }.ToFrozenSet();

    private static readonly FrozenSet<RadioAccessTechnology> DefaultLegacyRadioAccessTechnologies =
        new[] { RadioAccessTechnology.Umts, RadioAccessTechnology.Gsm }.ToFrozenSet();

    private static readonly FrozenSet<DeviceMotionState> DefaultStationaryMotionStates =
        new[] { DeviceMotionState.Stationary, DeviceMotionState.Walking }.ToFrozenSet();

    private static readonly FrozenSet<DeviceMotionState> DefaultBenignMotionStates =
        new[] { DeviceMotionState.Moving, DeviceMotionState.InVehicle }.ToFrozenSet();

    public int MinimumObservationCount { get; init; } = 4;

    public TimeSpan MinimumWindowDuration { get; init; } = TimeSpan.FromSeconds(30D);

    public int MinimumModernRatHistoryCount { get; init; } = 2;

    public int SuspiciousDowngradeCount { get; init; } = 1;

    public int HighRiskDowngradeCount { get; init; } = 2;

    public TimeSpan SuspiciousLegacyDwellDuration { get; init; } = TimeSpan.FromSeconds(20D);

    public TimeSpan HighRiskLegacyDwellDuration { get; init; } = TimeSpan.FromSeconds(60D);

    public double SuspiciousDowngradesPerMinute { get; init; } = 1D;

    public double HighRiskDowngradesPerMinute { get; init; } = 2D;

    public IReadOnlySet<RadioAccessTechnology> ModernRadioAccessTechnologies { get; init; } =
        DefaultModernRadioAccessTechnologies;

    public IReadOnlySet<RadioAccessTechnology> LegacyRadioAccessTechnologies { get; init; } =
        DefaultLegacyRadioAccessTechnologies;

    public IReadOnlySet<DeviceMotionState> StationaryMotionStates { get; init; } =
        DefaultStationaryMotionStates;

    public IReadOnlySet<DeviceMotionState> BenignMotionStates { get; init; } =
        DefaultBenignMotionStates;
}
