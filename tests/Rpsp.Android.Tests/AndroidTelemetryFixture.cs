using Rpsp.Core.Observations;

namespace Rpsp.Android.Tests;

internal static class AndroidTelemetryFixture
{
    public static readonly DateTimeOffset FixedObservedAt =
        new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    public static AndroidPermissionState GrantedState(
        AndroidTelemetryAvailability availability = AndroidTelemetryAvailability.Available)
    {
        return new AndroidPermissionState(
            AndroidPermissionStatus.Granted,
            IsLocationServiceEnabled: true,
            availability);
    }

    public static AndroidPermissionState DeniedState()
    {
        return new AndroidPermissionState(
            AndroidPermissionStatus.Denied,
            IsLocationServiceEnabled: true,
            AndroidTelemetryAvailability.PermissionDenied);
    }

    public static AndroidTelemetrySnapshot CompleteLteSnapshot(
        AndroidSubscriptionDescriptor? subscription = null,
        DateTimeOffset? observedAt = null)
    {
        return CreateSnapshot(
            GrantedState(),
            AndroidTelemetryAvailability.Available,
            [
                new AndroidCellInfoSnapshot(
                    CellRole.Serving,
                    RadioAccessTechnology.Lte,
                    "android-synthetic-serving",
                    new AndroidSignalStrengthSnapshot(RsrpDbm: -82D, RsrqDb: -9D, SinrDb: 18D),
                    ObservationAvailability.Available,
                    IsRegistered: true,
                    PhysicalCellId: 101,
                    ChannelNumber: 6300),
                new AndroidCellInfoSnapshot(
                    CellRole.Neighbor,
                    RadioAccessTechnology.Lte,
                    "android-synthetic-neighbor",
                    new AndroidSignalStrengthSnapshot(RsrpDbm: -91D, RsrqDb: -11D, SinrDb: 12D),
                    ObservationAvailability.Available,
                    IsRegistered: false,
                    PhysicalCellId: 202,
                    ChannelNumber: 6300)
            ],
            new AndroidServiceStateSnapshot(
                IsInService: true,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte),
            subscription,
            observedAt);
    }

    public static AndroidTelemetrySnapshot UnavailableLteMetricsSnapshot()
    {
        return CreateSnapshot(
            GrantedState(),
            AndroidTelemetryAvailability.PartiallyAvailable,
            [
                new AndroidCellInfoSnapshot(
                    CellRole.Serving,
                    RadioAccessTechnology.Lte,
                    "android-lte-unavailable-metrics",
                    new AndroidSignalStrengthSnapshot(),
                    ObservationAvailability.PartiallyAvailable,
                    IsRegistered: true)
            ],
            new AndroidServiceStateSnapshot(
                IsInService: true,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte));
    }

    public static AndroidTelemetrySnapshot UnavailableNrMetricsSnapshot()
    {
        return CreateSnapshot(
            GrantedState(),
            AndroidTelemetryAvailability.PartiallyAvailable,
            [
                new AndroidCellInfoSnapshot(
                    CellRole.Serving,
                    RadioAccessTechnology.Nr,
                    "android-nr-unavailable-metrics",
                    new AndroidSignalStrengthSnapshot(),
                    ObservationAvailability.PartiallyAvailable,
                    IsRegistered: true)
            ],
            new AndroidServiceStateSnapshot(
                IsInService: true,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Nr));
    }

    public static AndroidTelemetrySnapshot ServiceStateUnavailableSnapshot()
    {
        return CreateSnapshot(
            GrantedState(),
            AndroidTelemetryAvailability.PartiallyAvailable,
            [
                new AndroidCellInfoSnapshot(
                    CellRole.Serving,
                    RadioAccessTechnology.Lte,
                    "android-service-unavailable-serving",
                    new AndroidSignalStrengthSnapshot(RsrpDbm: -84D),
                    ObservationAvailability.Available,
                    IsRegistered: true)
            ]);
    }

    public static AndroidTelemetrySnapshot NeighborOnlySnapshot()
    {
        return CreateSnapshot(
            GrantedState(),
            AndroidTelemetryAvailability.PartiallyAvailable,
            [
                new AndroidCellInfoSnapshot(
                    CellRole.Neighbor,
                    RadioAccessTechnology.Lte,
                    "android-neighbor-only-lte",
                    new AndroidSignalStrengthSnapshot(RsrpDbm: -88D, RsrqDb: -10D, SinrDb: 12D),
                    ObservationAvailability.Available,
                    IsRegistered: false),
                new AndroidCellInfoSnapshot(
                    CellRole.Neighbor,
                    RadioAccessTechnology.Nr,
                    "android-neighbor-only-nr",
                    new AndroidSignalStrengthSnapshot(RsrpDbm: -92D, RsrqDb: -12D, SinrDb: 8D),
                    ObservationAvailability.Available,
                    IsRegistered: false)
            ],
            new AndroidServiceStateSnapshot(
                IsInService: true,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Nr));
    }

    public static AndroidTelemetrySnapshot MixedLteNrDisplayAmbiguitySnapshot()
    {
        return CreateSnapshot(
            GrantedState(),
            AndroidTelemetryAvailability.Available,
            [
                new AndroidCellInfoSnapshot(
                    CellRole.Serving,
                    RadioAccessTechnology.Lte,
                    "android-display-serving-lte",
                    new AndroidSignalStrengthSnapshot(RsrpDbm: -80D, RsrqDb: -9D, SinrDb: 20D),
                    ObservationAvailability.Available,
                    IsRegistered: true),
                new AndroidCellInfoSnapshot(
                    CellRole.Neighbor,
                    RadioAccessTechnology.Nr,
                    "android-display-neighbor-nr",
                    new AndroidSignalStrengthSnapshot(RsrpDbm: -86D, RsrqDb: -11D, SinrDb: 16D),
                    ObservationAvailability.Available,
                    IsRegistered: false)
            ],
            new AndroidServiceStateSnapshot(
                IsInService: true,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Nr),
            new AndroidSubscriptionDescriptor(
                "local-display-ambiguous",
                SlotIndex: null,
                IsAmbiguous: true));
    }

    public static AndroidTelemetrySnapshot FromReplayObservation(
        RadioObservation observation,
        AndroidSubscriptionDescriptor? subscription = null)
    {
        ArgumentNullException.ThrowIfNull(observation);

        AndroidCellInfoSnapshot[] cells = observation.CellSnapshots
            .Select(static cell => new AndroidCellInfoSnapshot(
                cell.Role,
                cell.RadioAccessTechnology,
                cell.Identifier?.NetworkCellId,
                cell.Signal is null
                    ? null
                    : new AndroidSignalStrengthSnapshot(
                        cell.Signal.RsrpDbm,
                        cell.Signal.RsrqDb,
                        cell.Signal.SinrDb),
                cell.Availability,
                cell.IsRegistered,
                cell.Identifier?.PhysicalCellId,
                cell.Identifier?.ChannelNumber))
            .ToArray();

        AndroidServiceStateSnapshot? serviceState = observation.ServiceState is null
            ? null
            : new AndroidServiceStateSnapshot(
                observation.ServiceState.IsInService ?? false,
                observation.ServiceState.VoiceRadioAccessTechnology ?? RadioAccessTechnology.Unknown,
                observation.ServiceState.DataRadioAccessTechnology ?? RadioAccessTechnology.Unknown);

        return CreateSnapshot(
            GrantedState(),
            AndroidTelemetryAvailability.Available,
            cells,
            serviceState,
            subscription,
            observation.ObservedAt);
    }

    public static AndroidTelemetrySnapshot CreateSnapshot(
        AndroidPermissionState permissionState,
        AndroidTelemetryAvailability availability,
        IReadOnlyList<AndroidCellInfoSnapshot>? cells = null,
        AndroidServiceStateSnapshot? serviceState = null,
        AndroidSubscriptionDescriptor? subscription = null,
        DateTimeOffset? observedAt = null)
    {
        return new AndroidTelemetrySnapshot(
            observedAt ?? FixedObservedAt,
            permissionState,
            availability,
            subscription ?? new AndroidSubscriptionDescriptor("local-alias-0"),
            cells ?? [],
            serviceState);
    }
}
