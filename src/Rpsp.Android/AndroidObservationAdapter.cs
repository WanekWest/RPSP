using System.Globalization;
using Rpsp.Core.Observations;

namespace Rpsp.Android;

public sealed record AndroidObservationAdapterOptions(
    string ObservationIdPrefix = "android-adapter");

public enum AndroidPermissionStatus
{
    Unknown = 0,
    NotRequested = 1,
    Denied = 2,
    Granted = 3,
    Unsupported = 4
}

public sealed record AndroidPermissionState(
    AndroidPermissionStatus Status,
    bool IsLocationServiceEnabled,
    AndroidTelemetryAvailability Availability,
    string? Reason = null)
{
    public bool CanReadPassiveTelemetry =>
        Status == AndroidPermissionStatus.Granted
        && IsLocationServiceEnabled
        && Availability == AndroidTelemetryAvailability.Available;
}

public enum AndroidTelemetryAvailability
{
    Unknown = 0,
    Unavailable = 1,
    PermissionDenied = 2,
    PartiallyAvailable = 3,
    Available = 4
}

public sealed record AndroidSubscriptionDescriptor(
    string LocalSubscriptionAlias,
    int? SlotIndex = null,
    bool IsAmbiguous = false);

public sealed record AndroidSignalStrengthSnapshot(
    double? RsrpDbm = null,
    double? RsrqDb = null,
    double? SinrDb = null);

public sealed record AndroidServiceStateSnapshot(
    bool IsInService,
    RadioAccessTechnology VoiceRadioAccessTechnology,
    RadioAccessTechnology DataRadioAccessTechnology);

public sealed record AndroidCellInfoSnapshot(
    CellRole Role,
    RadioAccessTechnology RadioAccessTechnology,
    string? CellKey,
    AndroidSignalStrengthSnapshot? SignalStrength = null,
    ObservationAvailability Availability = ObservationAvailability.Unknown,
    bool? IsRegistered = null,
    int? PhysicalCellId = null,
    int? ChannelNumber = null);

public sealed record AndroidTelemetrySnapshot(
    DateTimeOffset ObservedAt,
    AndroidPermissionState PermissionState,
    AndroidTelemetryAvailability Availability,
    AndroidSubscriptionDescriptor Subscription,
    IReadOnlyList<AndroidCellInfoSnapshot> Cells,
    AndroidServiceStateSnapshot? ServiceState = null);

public interface IAndroidTelemetrySource
{
    ValueTask<IReadOnlyList<AndroidTelemetrySnapshot>> ReadAsync(
        CancellationToken cancellationToken = default);
}

public interface IAndroidObservationMapper
{
    RadioObservation? Map(
        AndroidTelemetrySnapshot snapshot,
        AndroidObservationAdapterOptions options,
        int sequenceNumber);
}

public sealed class AndroidObservationAdapter
{
    private readonly IAndroidTelemetrySource _telemetrySource;
    private readonly IAndroidObservationMapper _mapper;
    private readonly AndroidObservationAdapterOptions _options;

    public AndroidObservationAdapter(
        IAndroidTelemetrySource telemetrySource,
        IAndroidObservationMapper mapper,
        AndroidObservationAdapterOptions? options = null)
    {
        _telemetrySource = telemetrySource ?? throw new ArgumentNullException(nameof(telemetrySource));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _options = options ?? new AndroidObservationAdapterOptions();
    }

    public async ValueTask<IReadOnlyList<RadioObservation>> ReadObservationsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AndroidTelemetrySnapshot> snapshots = await _telemetrySource
            .ReadAsync(cancellationToken)
            .ConfigureAwait(false);
        var observations = new List<RadioObservation>(snapshots.Count);
        int sequenceNumber = 0;

        foreach (AndroidTelemetrySnapshot snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!snapshot.PermissionState.CanReadPassiveTelemetry
                || snapshot.Availability == AndroidTelemetryAvailability.PermissionDenied
                || snapshot.Availability == AndroidTelemetryAvailability.Unavailable)
            {
                continue;
            }

            RadioObservation? observation = _mapper.Map(snapshot, _options, sequenceNumber);

            if (observation is not null)
            {
                observations.Add(observation);
                sequenceNumber++;
            }
        }

        return observations;
    }
}

public sealed class AndroidObservationMapper : IAndroidObservationMapper
{
    public RadioObservation? Map(
        AndroidTelemetrySnapshot snapshot,
        AndroidObservationAdapterOptions options,
        int sequenceNumber)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(options);

        if (!snapshot.PermissionState.CanReadPassiveTelemetry)
        {
            return null;
        }

        CellSnapshot[] cells = snapshot.Cells
            .Select(CreateCellSnapshot)
            .ToArray();

        return new RadioObservation(
            CreateObservationId(options, snapshot.Subscription, sequenceNumber),
            snapshot.ObservedAt,
            ObservationSourceKind.StockCore,
            cells,
            CreateServiceState(snapshot.ServiceState));
    }

    private static CellSnapshot CreateCellSnapshot(AndroidCellInfoSnapshot snapshot)
    {
        return new CellSnapshot(
            snapshot.Role,
            snapshot.RadioAccessTechnology,
            CreateIdentifier(snapshot),
            CreateSignal(snapshot.SignalStrength),
            snapshot.Availability,
            snapshot.IsRegistered);
    }

    private static CellIdentifier? CreateIdentifier(AndroidCellInfoSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.CellKey))
        {
            return null;
        }

        return new CellIdentifier(
            new Plmn("001", "01"),
            snapshot.CellKey,
            snapshot.PhysicalCellId,
            TrackingAreaCode: null,
            snapshot.ChannelNumber);
    }

    private static SignalSnapshot? CreateSignal(AndroidSignalStrengthSnapshot? snapshot)
    {
        return snapshot is null
            ? null
            : new SignalSnapshot(
                RsrpDbm: snapshot.RsrpDbm,
                RsrqDb: snapshot.RsrqDb,
                SinrDb: snapshot.SinrDb);
    }

    private static ServiceStateSnapshot? CreateServiceState(AndroidServiceStateSnapshot? snapshot)
    {
        return snapshot is null
            ? null
            : new ServiceStateSnapshot(
                IsInService: snapshot.IsInService,
                VoiceRadioAccessTechnology: snapshot.VoiceRadioAccessTechnology,
                DataRadioAccessTechnology: snapshot.DataRadioAccessTechnology);
    }

    private static string CreateObservationId(
        AndroidObservationAdapterOptions options,
        AndroidSubscriptionDescriptor subscription,
        int sequenceNumber)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{options.ObservationIdPrefix}:{subscription.LocalSubscriptionAlias}:{sequenceNumber}");
    }
}
