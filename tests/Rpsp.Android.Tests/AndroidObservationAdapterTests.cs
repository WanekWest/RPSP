using System.Reflection;
using Rpsp.Core.Observations;
using Rpsp.Evaluation;
using Rpsp.Evaluation.Scenarios;
using Xunit;

namespace Rpsp.Android.Tests;

public sealed class AndroidObservationAdapterTests
{
    [Fact]
    public async Task PermissionDeniedProducesNoObservations()
    {
        var source = new FakeAndroidTelemetrySource(
        [
            AndroidTelemetryFixture.CreateSnapshot(
                AndroidTelemetryFixture.DeniedState(),
                AndroidTelemetryAvailability.PermissionDenied)
        ]);
        var adapter = new AndroidObservationAdapter(source, new AndroidObservationMapper());

        IReadOnlyList<RadioObservation> observations = await adapter
            .ReadObservationsAsync();

        Assert.Empty(observations);
    }

    [Fact]
    public async Task PermissionGrantedButTelemetryUnavailableProducesNoObservations()
    {
        var source = new FakeAndroidTelemetrySource(
        [
            AndroidTelemetryFixture.CreateSnapshot(
                AndroidTelemetryFixture.GrantedState(AndroidTelemetryAvailability.Unavailable),
                AndroidTelemetryAvailability.Unavailable)
        ]);
        var adapter = new AndroidObservationAdapter(source, new AndroidObservationMapper());

        IReadOnlyList<RadioObservation> observations = await adapter
            .ReadObservationsAsync();

        Assert.Equal(1, source.ReadCount);
        Assert.Empty(observations);
    }

    [Fact]
    public void UnavailableMetricsMapToNullableSignalFields()
    {
        var mapper = new AndroidObservationMapper();
        AndroidTelemetrySnapshot snapshot = AndroidTelemetryFixture.CreateSnapshot(
            AndroidTelemetryFixture.GrantedState(),
            AndroidTelemetryAvailability.PartiallyAvailable,
            [
                new AndroidCellInfoSnapshot(
                    CellRole.Serving,
                    RadioAccessTechnology.Lte,
                    "android-synthetic-serving",
                    new AndroidSignalStrengthSnapshot(),
                    ObservationAvailability.PartiallyAvailable,
                    IsRegistered: true)
            ]);

        RadioObservation? observation = mapper.Map(
            snapshot,
            new AndroidObservationAdapterOptions(),
            sequenceNumber: 0);
        Assert.NotNull(observation);
        CellSnapshot cell = Assert.Single(observation.CellSnapshots);

        Assert.NotNull(cell.Signal);
        Assert.Null(cell.Signal.RsrpDbm);
        Assert.Null(cell.Signal.RsrqDb);
        Assert.Null(cell.Signal.SinrDb);
        Assert.Equal(ObservationAvailability.PartiallyAvailable, cell.Availability);
    }

    [Fact]
    public void UnavailableLteAndNrMetricsMapToNullableSignalFields()
    {
        var mapper = new AndroidObservationMapper();

        foreach (AndroidTelemetrySnapshot snapshot in new[]
        {
            AndroidTelemetryFixture.UnavailableLteMetricsSnapshot(),
            AndroidTelemetryFixture.UnavailableNrMetricsSnapshot()
        })
        {
            RadioObservation? observation = mapper.Map(
                snapshot,
                new AndroidObservationAdapterOptions(),
                sequenceNumber: 0);
            Assert.NotNull(observation);
            CellSnapshot cell = Assert.Single(observation.CellSnapshots);

            Assert.NotNull(cell.Signal);
            Assert.Null(cell.Signal.RsrpDbm);
            Assert.Null(cell.Signal.RsrqDb);
            Assert.Null(cell.Signal.SinrDb);
            Assert.Equal(ObservationAvailability.PartiallyAvailable, cell.Availability);
        }
    }

    [Fact]
    public void SignalMetricsMapToCoreFieldsWithoutPositionalDrift()
    {
        var mapper = new AndroidObservationMapper();

        RadioObservation? observation = mapper.Map(
            AndroidTelemetryFixture.CompleteLteSnapshot(),
            new AndroidObservationAdapterOptions(),
            sequenceNumber: 0);
        Assert.NotNull(observation);
        CellSnapshot serving = observation.CellSnapshots.Single(static cell => cell.Role == CellRole.Serving);

        Assert.NotNull(serving.Signal);
        Assert.Equal(-82D, serving.Signal.RsrpDbm);
        Assert.Equal(-9D, serving.Signal.RsrqDb);
        Assert.Equal(18D, serving.Signal.SinrDb);
        Assert.Null(serving.Signal.RssiDbm);
    }

    [Fact]
    public void ServingAndNeighborRolesMapToCoreCells()
    {
        var mapper = new AndroidObservationMapper();

        RadioObservation? observation = mapper.Map(
            AndroidTelemetryFixture.CompleteLteSnapshot(),
            new AndroidObservationAdapterOptions(),
            sequenceNumber: 0);
        Assert.NotNull(observation);

        Assert.Contains(observation.CellSnapshots, static cell => cell.Role == CellRole.Serving);
        Assert.Contains(observation.CellSnapshots, static cell => cell.Role == CellRole.Neighbor);
    }

    [Fact]
    public void RatAndServiceStateMapSafely()
    {
        var mapper = new AndroidObservationMapper();
        AndroidTelemetrySnapshot snapshot = AndroidTelemetryFixture.CreateSnapshot(
            AndroidTelemetryFixture.GrantedState(),
            AndroidTelemetryAvailability.Available,
            [
                new AndroidCellInfoSnapshot(
                    CellRole.Serving,
                    RadioAccessTechnology.Nr,
                    "android-nr-serving",
                    new AndroidSignalStrengthSnapshot(RsrpDbm: -76D, RsrqDb: -8D, SinrDb: 24D),
                    ObservationAvailability.Available,
                    IsRegistered: true),
                new AndroidCellInfoSnapshot(
                    CellRole.Neighbor,
                    RadioAccessTechnology.Lte,
                    "android-lte-neighbor",
                    new AndroidSignalStrengthSnapshot(RsrpDbm: -89D, RsrqDb: -10D, SinrDb: 13D),
                    ObservationAvailability.Available,
                    IsRegistered: false)
            ],
            new AndroidServiceStateSnapshot(
                IsInService: true,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Nr));

        RadioObservation? observation = mapper.Map(
            snapshot,
            new AndroidObservationAdapterOptions(),
            sequenceNumber: 0);
        Assert.NotNull(observation);

        Assert.Contains(observation.CellSnapshots, static cell => cell.RadioAccessTechnology == RadioAccessTechnology.Nr);
        Assert.Contains(observation.CellSnapshots, static cell => cell.RadioAccessTechnology == RadioAccessTechnology.Lte);
        Assert.Equal(RadioAccessTechnology.Lte, observation.ServiceState?.VoiceRadioAccessTechnology);
        Assert.Equal(RadioAccessTechnology.Nr, observation.ServiceState?.DataRadioAccessTechnology);
    }

    [Fact]
    public void UnknownRatMapsSafely()
    {
        var mapper = new AndroidObservationMapper();
        AndroidTelemetrySnapshot snapshot = AndroidTelemetryFixture.CreateSnapshot(
            AndroidTelemetryFixture.GrantedState(),
            AndroidTelemetryAvailability.PartiallyAvailable,
            [
                new AndroidCellInfoSnapshot(
                    CellRole.Serving,
                    RadioAccessTechnology.Unknown,
                    CellKey: null,
                    SignalStrength: null,
                    ObservationAvailability.PartiallyAvailable,
                    IsRegistered: true)
            ],
            new AndroidServiceStateSnapshot(
                IsInService: true,
                RadioAccessTechnology.Unknown,
                RadioAccessTechnology.Unknown));

        RadioObservation? observation = mapper.Map(
            snapshot,
            new AndroidObservationAdapterOptions(),
            sequenceNumber: 0);
        Assert.NotNull(observation);

        Assert.Equal(RadioAccessTechnology.Unknown, Assert.Single(observation.CellSnapshots).RadioAccessTechnology);
        Assert.Null(Assert.Single(observation.CellSnapshots).Identifier);
        Assert.Equal(RadioAccessTechnology.Unknown, observation.ServiceState?.DataRadioAccessTechnology);
    }

    [Fact]
    public async Task PermissionDeniedMidSequenceSkipsDeniedSnapshot()
    {
        AndroidTelemetrySnapshot first = AndroidTelemetryFixture.CompleteLteSnapshot(
            observedAt: AndroidTelemetryFixture.FixedObservedAt);
        AndroidTelemetrySnapshot denied = AndroidTelemetryFixture.CreateSnapshot(
            AndroidTelemetryFixture.DeniedState(),
            AndroidTelemetryAvailability.PermissionDenied,
            observedAt: AndroidTelemetryFixture.FixedObservedAt.AddSeconds(30D));
        AndroidTelemetrySnapshot second = AndroidTelemetryFixture.CompleteLteSnapshot(
            observedAt: AndroidTelemetryFixture.FixedObservedAt.AddSeconds(60D));
        var source = new FakeAndroidTelemetrySource([first, denied, second]);
        var adapter = new AndroidObservationAdapter(
            source,
            new AndroidObservationMapper(),
            new AndroidObservationAdapterOptions("android-fake"));

        IReadOnlyList<RadioObservation> observations = await adapter.ReadObservationsAsync();

        Assert.Equal(2, observations.Count);
        Assert.Equal("android-fake:local-alias-0:0", observations[0].ObservationId);
        Assert.Equal("android-fake:local-alias-0:1", observations[1].ObservationId);
        Assert.Equal(AndroidTelemetryFixture.FixedObservedAt.AddSeconds(60D), observations[1].ObservedAt);
    }

    [Fact]
    public void ServiceStateUnavailableRemainsNull()
    {
        var mapper = new AndroidObservationMapper();

        RadioObservation? observation = mapper.Map(
            AndroidTelemetryFixture.ServiceStateUnavailableSnapshot(),
            new AndroidObservationAdapterOptions(),
            sequenceNumber: 0);

        Assert.NotNull(observation);
        Assert.Null(observation.ServiceState);
        Assert.Single(observation.CellSnapshots);
    }

    [Fact]
    public void NeighborOnlyCellsMapWithoutServingInference()
    {
        var mapper = new AndroidObservationMapper();

        RadioObservation? observation = mapper.Map(
            AndroidTelemetryFixture.NeighborOnlySnapshot(),
            new AndroidObservationAdapterOptions(),
            sequenceNumber: 0);

        Assert.NotNull(observation);
        Assert.Equal(2, observation.CellSnapshots.Count);
        Assert.All(observation.CellSnapshots, static cell => Assert.Equal(CellRole.Neighbor, cell.Role));
        Assert.DoesNotContain(observation.CellSnapshots, static cell => cell.Role == CellRole.Serving);
    }

    [Fact]
    public void MixedLteNrDisplayAmbiguityPreservesObservedRats()
    {
        var mapper = new AndroidObservationMapper();
        AndroidTelemetrySnapshot snapshot = AndroidTelemetryFixture.MixedLteNrDisplayAmbiguitySnapshot();

        RadioObservation? observation = mapper.Map(
            snapshot,
            new AndroidObservationAdapterOptions("android-fake"),
            sequenceNumber: 0);

        Assert.NotNull(observation);
        Assert.Contains(observation.CellSnapshots, static cell => cell.RadioAccessTechnology == RadioAccessTechnology.Lte);
        Assert.Contains(observation.CellSnapshots, static cell => cell.RadioAccessTechnology == RadioAccessTechnology.Nr);
        Assert.Equal(RadioAccessTechnology.Lte, observation.ServiceState?.VoiceRadioAccessTechnology);
        Assert.Equal(RadioAccessTechnology.Nr, observation.ServiceState?.DataRadioAccessTechnology);
        Assert.Equal("android-fake:local-display-ambiguous:0", observation.ObservationId);
        Assert.True(snapshot.Subscription.IsAmbiguous);
    }

    [Fact]
    public async Task MultiSubscriptionSnapshotsUseLocalAliasesOnly()
    {
        var firstSubscription = new AndroidSubscriptionDescriptor("local-alias-1", SlotIndex: 0);
        var secondSubscription = new AndroidSubscriptionDescriptor(
            "local-alias-2",
            SlotIndex: 1,
            IsAmbiguous: true);
        var source = new FakeAndroidTelemetrySource(
        [
            AndroidTelemetryFixture.CompleteLteSnapshot(firstSubscription),
            AndroidTelemetryFixture.CompleteLteSnapshot(secondSubscription, AndroidTelemetryFixture.FixedObservedAt.AddSeconds(30D))
        ]);
        var adapter = new AndroidObservationAdapter(
            source,
            new AndroidObservationMapper(),
            new AndroidObservationAdapterOptions("android-fake"));

        IReadOnlyList<RadioObservation> observations = await adapter.ReadObservationsAsync();

        Assert.Equal(2, observations.Count);
        Assert.Contains(observations, static observation => observation.ObservationId == "android-fake:local-alias-1:0");
        Assert.Contains(observations, static observation => observation.ObservationId == "android-fake:local-alias-2:1");
        Assert.True(secondSubscription.IsAmbiguous);
        Assert.DoesNotContain(
            secondSubscription.GetType().GetProperties().Select(static property => property.Name),
            static propertyName => propertyName.Contains("Subscriber", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SubscriptionDescriptorRemainsNonIdentifying()
    {
        var descriptor = new AndroidSubscriptionDescriptor(
            "local-alias-privacy",
            SlotIndex: 1,
            IsAmbiguous: true);
        string serialized = string.Join(
            '|',
            descriptor.LocalSubscriptionAlias,
            descriptor.SlotIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none",
            descriptor.IsAmbiguous.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Contains("local-alias-privacy", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(string.Concat("IM", "SI"), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(string.Concat("IM", "EI"), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(string.Concat("MS", "ISDN"), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(string.Concat("sub", "scriber"), serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FakeAndroidSnapshotHasReplayParityForEquivalentObservationShape()
    {
        ReplayTrace replayTrace = new RadioObservationScenarioBuilder(ReplayScenarioKind.StationaryStableServingCell)
            .AddServingCellWithNeighbor(
                TimeSpan.Zero,
                DeviceMotionState.Stationary,
                "android-replay-serving",
                "android-replay-neighbor",
                servingRsrpDbm: -82D,
                neighborRsrpDbm: -91D)
            .Build();
        RadioObservation replayObservation = Assert.Single(replayTrace.Frames).Observation;
        AndroidTelemetrySnapshot snapshot = AndroidTelemetryFixture.FromReplayObservation(replayObservation);
        var mapper = new AndroidObservationMapper();

        RadioObservation? mappedObservation = mapper.Map(
            snapshot,
            new AndroidObservationAdapterOptions("android-fake"),
            sequenceNumber: 0);
        Assert.NotNull(mappedObservation);

        AssertEquivalentReplayShape(replayObservation, mappedObservation);
    }

    [Fact]
    public void ProjectDoesNotReferenceAndroidRuntimeAssemblies()
    {
        AssemblyName[] references = typeof(AndroidObservationAdapter)
            .Assembly
            .GetReferencedAssemblies();

        Assert.DoesNotContain(
            references,
            static reference => string.Equals(reference.Name, "Mono.Android", StringComparison.Ordinal)
                || string.Equals(reference.Name, "Xamarin.Android", StringComparison.Ordinal));
    }

    [Fact]
    public void PublicModelPropertyNamesAvoidSensitiveIdentityTerms()
    {
        string[] sensitiveTerms =
        [
            "Subscriber",
            "Hardware",
            "PhoneNumber",
            "Account",
            "SimSerial",
            "PermanentIdentity"
        ];
        Type[] publicTypes = typeof(AndroidObservationAdapter)
            .Assembly
            .GetExportedTypes()
            .Where(static type => type.Namespace == "Rpsp.Android")
            .ToArray();

        foreach (Type publicType in publicTypes)
        {
            foreach (PropertyInfo property in publicType.GetProperties())
            {
                foreach (string sensitiveTerm in sensitiveTerms)
                {
                    Assert.DoesNotContain(
                        sensitiveTerm,
                        property.Name,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    private static void AssertEquivalentReplayShape(
        RadioObservation expectedReplayObservation,
        RadioObservation actualMappedObservation)
    {
        Assert.Equal(expectedReplayObservation.ObservedAt, actualMappedObservation.ObservedAt);
        Assert.Equal(ObservationSourceKind.StockCore, actualMappedObservation.SourceKind);
        Assert.Equal(
            expectedReplayObservation.CellSnapshots.Count,
            actualMappedObservation.CellSnapshots.Count);

        for (int index = 0; index < expectedReplayObservation.CellSnapshots.Count; index++)
        {
            CellSnapshot expectedCell = expectedReplayObservation.CellSnapshots[index];
            CellSnapshot actualCell = actualMappedObservation.CellSnapshots[index];

            Assert.Equal(expectedCell.Role, actualCell.Role);
            Assert.Equal(expectedCell.RadioAccessTechnology, actualCell.RadioAccessTechnology);
            Assert.Equal(expectedCell.Identifier?.NetworkCellId, actualCell.Identifier?.NetworkCellId);
            Assert.Equal(expectedCell.Identifier?.PhysicalCellId, actualCell.Identifier?.PhysicalCellId);
            Assert.Equal(expectedCell.Identifier?.ChannelNumber, actualCell.Identifier?.ChannelNumber);
            Assert.Equal(expectedCell.Signal?.RsrpDbm, actualCell.Signal?.RsrpDbm);
            Assert.Equal(expectedCell.Signal?.RsrqDb, actualCell.Signal?.RsrqDb);
            Assert.Equal(expectedCell.Signal?.SinrDb, actualCell.Signal?.SinrDb);
            Assert.Equal(expectedCell.Availability, actualCell.Availability);
            Assert.Equal(expectedCell.IsRegistered, actualCell.IsRegistered);
        }

        Assert.Equal(
            expectedReplayObservation.ServiceState?.IsInService,
            actualMappedObservation.ServiceState?.IsInService);
        Assert.Equal(
            expectedReplayObservation.ServiceState?.VoiceRadioAccessTechnology,
            actualMappedObservation.ServiceState?.VoiceRadioAccessTechnology);
        Assert.Equal(
            expectedReplayObservation.ServiceState?.DataRadioAccessTechnology,
            actualMappedObservation.ServiceState?.DataRadioAccessTechnology);
    }
}
