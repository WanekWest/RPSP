using Rpsp.Core.Baselines;
using Rpsp.Core.Observations;
using Rpsp.Core.Pipeline;
using Xunit;

namespace Rpsp.Persistence.Tests;

public sealed class InMemoryPipelineIntegrationTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 7, 2, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StoredBaselineProfileIsUsedByPipeline()
    {
        PersistenceHarness harness = CreateHarness();
        harness.SaveProfileWithTrustedSignatures("profile-a", "known-a", "known-b", "known-c");
        harness.AddObservations(CreateStableObservations());

        PipelineInputSnapshot input = harness.InputProvider.CreateInput(
            "profile-a",
            Start.AddHours(1D));
        RpspDetectionPipelineResult result = new RpspDetectionPipeline()
            .Run(input.Observations, input.Baseline);

        Assert.Equal("profile-a", result.Baseline.ProfileId);
        Assert.Equal(3, result.Baseline.TrustedCells.Count);
        Assert.Equal(6, result.Observations.Count);
        Assert.True(result.CompositeAssessment.RiskScore.Value < 0.25D);
    }

    [Fact]
    public void StoredTrustedSignaturesDriveUnknownStrongCellAndNeighborGraphThroughPipeline()
    {
        PersistenceHarness harness = CreateHarness();
        harness.SaveProfileWithTrustedSignatures("profile-a", "known-a", "known-b", "known-c");
        harness.AddObservations(CreateRepeatedUnknownGraphDriftObservations());

        PipelineInputSnapshot input = harness.InputProvider.CreateInput(
            "profile-a",
            Start.AddHours(1D));
        RpspDetectionPipelineResult result = new RpspDetectionPipeline()
            .Run(input.Observations, input.Baseline);

        Assert.True(ContributionScore(result, "UnknownStrongCellDetector") >= 0.8D);
        Assert.True(ContributionScore(result, "NeighborGraphAnomalyDetector") >= 0.8D);
        Assert.True(result.CompositeAssessment.RiskScore.Value >= 0.8D);
    }

    [Fact]
    public void RingBufferObservationsReplayIntoPipeline()
    {
        PersistenceHarness harness = CreateHarness(maxObservationCount: 4);
        harness.SaveProfileWithTrustedSignatures("profile-a", "known-a", "known-b", "known-c");
        harness.AddObservations(CreateStableObservations());

        PipelineInputSnapshot input = harness.InputProvider.CreateInput(
            "profile-a",
            Start.AddHours(1D));
        RpspDetectionPipelineResult result = new RpspDetectionPipeline()
            .Run(input.Observations, input.Baseline);

        Assert.Equal(4, input.Observations.Count);
        Assert.Equal("stable-2", input.Observations[0].ObservationId);
        Assert.Equal("stable-5", input.Observations[^1].ObservationId);
        Assert.True(result.CompositeAssessment.RiskScore.Value < 0.25D);
    }

    [Fact]
    public void RetentionCleanupRunsBeforePipelineExecution()
    {
        PersistenceHarness harness = CreateHarness();
        harness.SaveProfileWithTrustedSignatures("profile-a", "known-a", "known-b", "known-c");

        foreach (RadioObservation observation in CreateRepeatedUnknownGraphDriftObservations())
        {
            harness.Observations.Add(observation, Start.AddMinutes(-1D));
        }

        harness.AddObservations(CreateStableObservations());

        PipelineInputSnapshot input = harness.InputProvider.CreateInput(
            "profile-a",
            Start);
        RpspDetectionPipelineResult result = new RpspDetectionPipeline()
            .Run(input.Observations, input.Baseline);

        Assert.NotNull(input.RetentionCleanup);
        Assert.Equal(8, input.RetentionCleanup.RemovedRecordCount);
        Assert.Equal(6, input.Observations.Count);
        Assert.True(result.CompositeAssessment.RiskScore.Value < 0.25D);
    }

    [Fact]
    public void PipelineObservationStoreRejectsRawCellIdentifiers()
    {
        var observations = new InMemoryPipelineObservationStore();

        Assert.Throws<ArgumentException>(() => observations.Add(
            CreateRawIdentifierObservation(),
            Start.AddMinutes(30D)));

        Assert.Empty(observations.Snapshot());
    }

    [Fact]
    public void PipelineObservationStoreRetainsOnlyPseudonymizedCellIdentifiers()
    {
        var observations = new InMemoryPipelineObservationStore();
        RadioObservation safeObservation = CreateStableObservations()[0];

        observations.Add(safeObservation, Start.AddMinutes(30D));

        RadioObservation retained = Assert.Single(observations.Snapshot());
        foreach (CellSnapshot cell in retained.CellSnapshots)
        {
            Assert.Null(cell.Identifier?.Plmn);
            Assert.StartsWith("cell-pseudonym:", cell.Identifier?.NetworkCellId, StringComparison.Ordinal);
        }
    }

    private static PersistenceHarness CreateHarness(int maxObservationCount = 32)
    {
        var profiles = new InMemoryBaselineProfileStore();
        var signatures = new InMemoryTrustedCellSignatureStore();
        var observations = new InMemoryPipelineObservationStore(
            new PipelineObservationStoreOptions(MaxObservationCount: maxObservationCount));
        var inputProvider = new InMemoryPipelineInputProvider(profiles, signatures, observations);

        return new PersistenceHarness(profiles, signatures, observations, inputProvider);
    }

    private static RadioObservation[] CreateStableObservations()
    {
        return Enumerable.Range(0, 6)
            .Select(index => CreateObservation(
                $"stable-{index}",
                Start.AddSeconds(index * 30D),
                "known-a",
                ["known-b", "known-c"],
                servingRsrpDbm: -86D))
            .ToArray();
    }

    private static RadioObservation[] CreateRepeatedUnknownGraphDriftObservations()
    {
        return Enumerable.Range(0, 8)
            .Select(index => CreateObservation(
                $"unknown-drift-{index}",
                Start.AddSeconds(index * 30D),
                "unknown-strong",
                ["unknown-x", "unknown-y", "unknown-z"],
                servingRsrpDbm: -62D))
            .ToArray();
    }

    private static RadioObservation CreateObservation(
        string observationId,
        DateTimeOffset observedAt,
        string servingCell,
        IReadOnlyList<string> neighborCells,
        double servingRsrpDbm)
    {
        var cells = new List<CellSnapshot>
        {
            CreateCell(CellRole.Serving, servingCell, servingRsrpDbm, isRegistered: true)
        };

        foreach (string neighborCell in neighborCells)
        {
            cells.Add(CreateCell(
                CellRole.Neighbor,
                neighborCell,
                servingRsrpDbm - 5D,
                isRegistered: false));
        }

        return new RadioObservation(
            observationId,
            observedAt,
            ObservationSourceKind.Synthetic,
            cells,
            new ServiceStateSnapshot(
                IsInService: true,
                VoiceRadioAccessTechnology: RadioAccessTechnology.Lte,
                DataRadioAccessTechnology: RadioAccessTechnology.Lte),
            deviceMotion: new DeviceMotionSnapshot(DeviceMotionState.Stationary, confidence: 0.95D));
    }

    private static CellSnapshot CreateCell(
        CellRole role,
        string cellId,
        double rsrpDbm,
        bool isRegistered)
    {
        return new CellSnapshot(
            role,
            RadioAccessTechnology.Lte,
            new CellIdentifier(
                Plmn: null,
                NetworkCellId: Pseudonym(cellId),
                ChannelNumber: 6300),
            new SignalSnapshot(RsrpDbm: rsrpDbm),
            ObservationAvailability.Available,
            IsRegistered: isRegistered);
    }

    private static RadioObservation CreateRawIdentifierObservation()
    {
        var cell = new CellSnapshot(
            CellRole.Serving,
            RadioAccessTechnology.Lte,
            new CellIdentifier(
                new Plmn("001", "01"),
                "raw-cell-a",
                ChannelNumber: 6300),
            new SignalSnapshot(RsrpDbm: -80D),
            ObservationAvailability.Available,
            IsRegistered: true);

        return new RadioObservation(
            "raw-observation",
            Start,
            ObservationSourceKind.Synthetic,
            [cell],
            new ServiceStateSnapshot(IsInService: true));
    }

    private static TrustedCellSignature CreateTrustedSignature(string cellId)
    {
        return new TrustedCellSignature(
            $"trusted:{cellId}",
            RadioAccessTechnology.Lte,
            NetworkCellPseudonym: Pseudonym(cellId),
            ChannelNumber: 6300,
            FirstTrustedAt: Start.AddDays(-7D),
            LastSeenAt: Start);
    }

    private static string Pseudonym(string cellId)
    {
        return $"cell-pseudonym:{cellId}";
    }

    private static double ContributionScore(RpspDetectionPipelineResult result, string detectorName)
    {
        return result.DetectorContributions.Single(contribution =>
            string.Equals(contribution.DetectorName, detectorName, StringComparison.Ordinal))
            .RiskScore
            .Value;
    }

    private sealed record PersistenceHarness(
        InMemoryBaselineProfileStore Profiles,
        InMemoryTrustedCellSignatureStore Signatures,
        InMemoryPipelineObservationStore Observations,
        InMemoryPipelineInputProvider InputProvider)
    {
        public void SaveProfileWithTrustedSignatures(string profileId, params string[] cellIds)
        {
            Profiles.Save(
                new BaselineProfile(
                    profileId,
                    Start.AddDays(-7D),
                    Start,
                    []),
                Start.AddDays(7D));

            foreach (string cellId in cellIds)
            {
                Signatures.Upsert(profileId, CreateTrustedSignature(cellId), Start.AddDays(7D));
            }
        }

        public void AddObservations(IEnumerable<RadioObservation> observations)
        {
            foreach (RadioObservation observation in observations)
            {
                Observations.Add(observation, Start.AddHours(2D));
            }
        }
    }
}
