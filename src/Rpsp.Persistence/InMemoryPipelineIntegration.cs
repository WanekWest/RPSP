using Rpsp.Core.Baselines;
using Rpsp.Core.Observations;

namespace Rpsp.Persistence;

public interface IPipelineObservationStore : IRetentionManagedStore
{
    void Add(RadioObservation observation, DateTimeOffset expiresAt);

    IReadOnlyList<RadioObservation> Snapshot();
}

public sealed record PipelineObservationStoreOptions(
    string RequiredNetworkCellPseudonymPrefix = "cell-pseudonym:",
    bool RequireNullPlmn = true,
    int MaxObservationCount = 128);

public sealed record PipelineInputSnapshot(
    BaselineProfile Baseline,
    IReadOnlyList<RadioObservation> Observations,
    RetentionCleanupResult? RetentionCleanup);

public sealed class InMemoryPipelineObservationStore : IPipelineObservationStore
{
    private readonly int _maxCount;
    private readonly PipelineObservationStoreOptions _options;
    private readonly Queue<RetainedValue<RadioObservation>> _observations = new();

    public InMemoryPipelineObservationStore(PipelineObservationStoreOptions? options = null)
    {
        _options = options ?? new PipelineObservationStoreOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(_options.MaxObservationCount, 1);

        _maxCount = _options.MaxObservationCount;
    }

    public void Add(RadioObservation observation, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(observation);

        ValidateObservationForRetention(observation, _options);
        _observations.Enqueue(new RetainedValue<RadioObservation>(CopyObservation(observation), expiresAt));

        while (_observations.Count > _maxCount)
        {
            _observations.Dequeue();
        }
    }

    public IReadOnlyList<RadioObservation> Snapshot()
    {
        return _observations
            .Select(static retained => retained.Value)
            .ToArray();
    }

    public int RemoveExpired(DateTimeOffset currentTime)
    {
        RetainedValue<RadioObservation>[] retained = _observations
            .Where(observation => observation.ExpiresAt > currentTime)
            .ToArray();
        int removed = _observations.Count - retained.Length;

        _observations.Clear();

        foreach (RetainedValue<RadioObservation> observation in retained)
        {
            _observations.Enqueue(observation);
        }

        return removed;
    }

    private static void ValidateObservationForRetention(
        RadioObservation observation,
        PipelineObservationStoreOptions options)
    {
        foreach (CellSnapshot cell in observation.CellSnapshots)
        {
            CellIdentifier? identifier = cell.Identifier;

            if (identifier is null)
            {
                continue;
            }

            if (options.RequireNullPlmn && identifier.Plmn is not null)
            {
                throw new ArgumentException(
                    "Persisted pipeline observations must not retain raw PLMN values.",
                    nameof(observation));
            }

            if (identifier.NetworkCellId is not null
                && !identifier.NetworkCellId.StartsWith(
                    options.RequiredNetworkCellPseudonymPrefix,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Persisted pipeline observations must use cell pseudonyms.",
                    nameof(observation));
            }
        }
    }

    private static RadioObservation CopyObservation(RadioObservation observation)
    {
        return new RadioObservation(
            observation.ObservationId,
            observation.ObservedAt,
            observation.SourceKind,
            observation.CellSnapshots.ToArray(),
            observation.ServiceState,
            observation.PhysicalChannels.ToArray(),
            observation.DeviceMotion);
    }
}

public sealed class InMemoryPipelineInputProvider
{
    private readonly IBaselineProfileStore _baselineProfiles;
    private readonly IPipelineObservationStore _observations;
    private readonly IRetentionPolicyEnforcer _retentionPolicyEnforcer;
    private readonly ITrustedCellSignatureStore _trustedSignatures;

    public InMemoryPipelineInputProvider(
        IBaselineProfileStore baselineProfiles,
        ITrustedCellSignatureStore trustedSignatures,
        IPipelineObservationStore observations,
        IRetentionPolicyEnforcer? retentionPolicyEnforcer = null)
    {
        _baselineProfiles = baselineProfiles ?? throw new ArgumentNullException(nameof(baselineProfiles));
        _trustedSignatures = trustedSignatures ?? throw new ArgumentNullException(nameof(trustedSignatures));
        _observations = observations ?? throw new ArgumentNullException(nameof(observations));
        _retentionPolicyEnforcer = retentionPolicyEnforcer ?? new RetentionPolicyEnforcer();
    }

    public PipelineInputSnapshot CreateInput(
        string profileId,
        DateTimeOffset currentTime,
        bool enforceRetention = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        RetentionCleanupResult? cleanup = enforceRetention
            ? _retentionPolicyEnforcer.Enforce(
                currentTime,
                _baselineProfiles,
                _trustedSignatures,
                _observations)
            : null;

        BaselineProfile profile = _baselineProfiles.FindByProfileId(profileId)
            ?? throw new InvalidOperationException("Baseline profile is unavailable.");
        IReadOnlyList<TrustedCellSignature> storedSignatures = _trustedSignatures.GetByProfile(profileId);
        BaselineProfile baseline = storedSignatures.Count == 0
            ? profile
            : new BaselineProfile(
                profile.ProfileId,
                profile.CreatedAt,
                profile.UpdatedAt,
                storedSignatures);

        return new PipelineInputSnapshot(
            baseline,
            _observations.Snapshot(),
            cleanup);
    }
}
