using Rpsp.Core.Baselines;

namespace Rpsp.Persistence;

public interface IBaselineProfileStore : IRetentionManagedStore
{
    void Save(BaselineProfile profile, DateTimeOffset expiresAt);

    BaselineProfile? FindByProfileId(string profileId);
}

public interface ITrustedCellSignatureStore : IRetentionManagedStore
{
    void Upsert(string profileId, TrustedCellSignature signature, DateTimeOffset expiresAt);

    IReadOnlyList<TrustedCellSignature> GetByProfile(string profileId);
}

public interface IObservationRingBuffer : IRetentionManagedStore
{
    void Add(ObservationSummary summary);

    IReadOnlyList<ObservationSummary> Snapshot();
}

public interface IRetentionManagedStore
{
    int RemoveExpired(DateTimeOffset currentTime);
}

public interface IRetentionPolicyEnforcer
{
    RetentionCleanupResult Enforce(DateTimeOffset currentTime, params IRetentionManagedStore[] stores);
}

public interface IPseudonymKeyProvider
{
    bool IsAvailable { get; }

    PseudonymKeyDescriptor GetCurrentKey();
}

public interface IDebugExportRedactor
{
    RedactedDebugExport Redact(IReadOnlyDictionary<string, string?> fields);
}

public interface IInMemoryDebugExportBuilder
{
    InMemoryDebugExport Build(
        IReadOnlyList<ObservationSummary> observations,
        IReadOnlyDictionary<string, string?> metadata,
        DateTimeOffset createdAt);
}

public sealed record ObservationSummary(
    string ObservationId,
    DateTimeOffset ObservedAt,
    IReadOnlyList<string> CellPseudonyms,
    DateTimeOffset ExpiresAt,
    string SourceKind);

public sealed record RetentionPolicyOptions(
    TimeSpan BaselineProfileRetention,
    TimeSpan TrustedSignatureRetention,
    TimeSpan ObservationSummaryRetention,
    int MaxObservationCount);

public sealed record RetentionCleanupResult(
    int StoreCount,
    int RemovedRecordCount);

public sealed record PseudonymKeyDescriptor(
    string KeyId,
    string Purpose,
    bool IsTestOnly);

public sealed record RedactedDebugExport(
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyList<string> RemovedFieldNames);

public sealed record DebugExportOptions(
    TimeSpan TimeBinSize,
    string ExportFormatVersion = "rpsp-debug-export-v1",
    string PrivacyTransformVersion = "batch-005-redaction-v1");

public sealed record DebugExportManifest(
    string ExportFormatVersion,
    string PrivacyTransformVersion,
    string TimeBinPolicy,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> IncludedRecordClasses,
    IReadOnlyList<string> ExcludedSensitiveClasses);

public sealed record RedactedObservationExport(
    string ExportObservationId,
    DateTimeOffset ObservedTimeBinStart,
    string TimeBinPolicy,
    string SourceKind,
    IReadOnlyList<string> CellPseudonyms,
    int RemovedCellReferenceCount);

public sealed record InMemoryDebugExport(
    DebugExportManifest Manifest,
    IReadOnlyList<RedactedObservationExport> Observations,
    RedactedDebugExport Metadata);

public sealed class InMemoryBaselineProfileStore : IBaselineProfileStore
{
    private readonly Dictionary<string, RetainedValue<BaselineProfile>> _profiles = new(StringComparer.Ordinal);

    public void Save(BaselineProfile profile, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _profiles[profile.ProfileId] = new RetainedValue<BaselineProfile>(profile, expiresAt);
    }

    public BaselineProfile? FindByProfileId(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        return _profiles.TryGetValue(profileId, out RetainedValue<BaselineProfile>? value)
            ? value.Value
            : null;
    }

    public int RemoveExpired(DateTimeOffset currentTime)
    {
        string[] expiredProfileIds = _profiles
            .Where(pair => pair.Value.ExpiresAt <= currentTime)
            .Select(static pair => pair.Key)
            .ToArray();

        foreach (string profileId in expiredProfileIds)
        {
            _profiles.Remove(profileId);
        }

        return expiredProfileIds.Length;
    }
}

public sealed class InMemoryTrustedCellSignatureStore : ITrustedCellSignatureStore
{
    private readonly Dictionary<string, List<RetainedValue<TrustedCellSignature>>> _signatures =
        new(StringComparer.Ordinal);

    public void Upsert(string profileId, TrustedCellSignature signature, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(signature);

        if (!_signatures.TryGetValue(profileId, out List<RetainedValue<TrustedCellSignature>>? profileSignatures))
        {
            profileSignatures = [];
            _signatures[profileId] = profileSignatures;
        }

        profileSignatures.RemoveAll(value => string.Equals(
            value.Value.SignatureId,
            signature.SignatureId,
            StringComparison.Ordinal));
        profileSignatures.Add(new RetainedValue<TrustedCellSignature>(signature, expiresAt));
    }

    public IReadOnlyList<TrustedCellSignature> GetByProfile(string profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

        return _signatures.TryGetValue(profileId, out List<RetainedValue<TrustedCellSignature>>? profileSignatures)
            ? profileSignatures.Select(static value => value.Value).ToArray()
            : [];
    }

    public int RemoveExpired(DateTimeOffset currentTime)
    {
        int removed = 0;

        foreach (List<RetainedValue<TrustedCellSignature>> profileSignatures in _signatures.Values)
        {
            removed += profileSignatures.RemoveAll(value => value.ExpiresAt <= currentTime);
        }

        string[] emptyProfileIds = _signatures
            .Where(static pair => pair.Value.Count == 0)
            .Select(static pair => pair.Key)
            .ToArray();

        foreach (string profileId in emptyProfileIds)
        {
            _signatures.Remove(profileId);
        }

        return removed;
    }
}

public sealed class InMemoryObservationRingBuffer : IObservationRingBuffer
{
    private readonly int _maxCount;
    private readonly Queue<ObservationSummary> _summaries = new();

    public InMemoryObservationRingBuffer(int maxCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);

        _maxCount = maxCount;
    }

    public void Add(ObservationSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        _summaries.Enqueue(summary);

        while (_summaries.Count > _maxCount)
        {
            _summaries.Dequeue();
        }
    }

    public IReadOnlyList<ObservationSummary> Snapshot()
    {
        return _summaries.ToArray();
    }

    public int RemoveExpired(DateTimeOffset currentTime)
    {
        ObservationSummary[] retained = _summaries
            .Where(summary => summary.ExpiresAt > currentTime)
            .ToArray();
        int removed = _summaries.Count - retained.Length;

        _summaries.Clear();

        foreach (ObservationSummary summary in retained)
        {
            _summaries.Enqueue(summary);
        }

        return removed;
    }
}

public sealed class RetentionPolicyEnforcer : IRetentionPolicyEnforcer
{
    public RetentionCleanupResult Enforce(DateTimeOffset currentTime, params IRetentionManagedStore[] stores)
    {
        ArgumentNullException.ThrowIfNull(stores);

        int removed = stores.Sum(store =>
        {
            ArgumentNullException.ThrowIfNull(store);

            return store.RemoveExpired(currentTime);
        });

        return new RetentionCleanupResult(stores.Length, removed);
    }
}

public sealed class InMemoryPseudonymKeyProvider : IPseudonymKeyProvider
{
    private readonly PseudonymKeyDescriptor _descriptor;

    public InMemoryPseudonymKeyProvider(PseudonymKeyDescriptor descriptor, bool isAvailable = true)
    {
        _descriptor = descriptor;
        IsAvailable = isAvailable;
    }

    public bool IsAvailable { get; }

    public PseudonymKeyDescriptor GetCurrentKey()
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Pseudonym key is unavailable.");
        }

        return _descriptor;
    }
}

public sealed class DebugExportRedactor : IDebugExportRedactor
{
    private static readonly string[] SensitiveFieldFragments =
    [
        "raw",
        "route",
        "account",
        "phone",
        "hardware",
        "subscriber",
        "deviceid"
    ];

    public RedactedDebugExport Redact(IReadOnlyDictionary<string, string?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var retained = new Dictionary<string, string>(StringComparer.Ordinal);
        var removed = new List<string>();

        foreach ((string key, string? value) in fields)
        {
            if (IsSensitiveFieldName(key) || value is null)
            {
                removed.Add(key);
                continue;
            }

            retained[key] = value;
        }

        return new RedactedDebugExport(retained, removed);
    }

    private static bool IsSensitiveFieldName(string key)
    {
        return SensitiveFieldFragments.Any(fragment =>
            key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class InMemoryDebugExportBuilder : IInMemoryDebugExportBuilder
{
    private const string CellPseudonymPrefix = "cell-pseudonym:";
    private readonly IDebugExportRedactor _redactor;
    private readonly DebugExportOptions _options;

    public InMemoryDebugExportBuilder(
        DebugExportOptions? options = null,
        IDebugExportRedactor? redactor = null)
    {
        _options = options ?? new DebugExportOptions(TimeSpan.FromMinutes(15D));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.TimeBinSize, TimeSpan.Zero);

        _redactor = redactor ?? new DebugExportRedactor();
    }

    public InMemoryDebugExport Build(
        IReadOnlyList<ObservationSummary> observations,
        IReadOnlyDictionary<string, string?> metadata,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(metadata);

        RedactedObservationExport[] exportedObservations = observations
            .Select(CreateObservationExport)
            .ToArray();
        DebugExportManifest manifest = CreateManifest(createdAt);

        return new InMemoryDebugExport(
            manifest,
            exportedObservations,
            _redactor.Redact(metadata));
    }

    private RedactedObservationExport CreateObservationExport(
        ObservationSummary observation,
        int index)
    {
        ArgumentNullException.ThrowIfNull(observation);

        string[] cellPseudonyms = observation.CellPseudonyms
            .Select((cellReference, cellIndex) => RedactCellReference(cellReference, cellIndex))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        int removedCellReferenceCount = observation.CellPseudonyms.Count - cellPseudonyms.Length;

        return new RedactedObservationExport(
            $"observation-{index:D4}",
            BinTime(observation.ObservedAt, _options.TimeBinSize),
            FormatTimeBinPolicy(_options.TimeBinSize),
            observation.SourceKind,
            cellPseudonyms,
            removedCellReferenceCount);
    }

    private DebugExportManifest CreateManifest(DateTimeOffset createdAt)
    {
        return new DebugExportManifest(
            _options.ExportFormatVersion,
            _options.PrivacyTransformVersion,
            FormatTimeBinPolicy(_options.TimeBinSize),
            createdAt,
            IncludedRecordClasses: ["redacted-observation-summary", "redacted-metadata"],
            ExcludedSensitiveClasses: ["raw-cell-fields", "raw-route-fields", "account-linked-fields", "hardware-fields"]);
    }

    private static string RedactCellReference(string cellReference, int index)
    {
        if (cellReference.StartsWith(CellPseudonymPrefix, StringComparison.Ordinal))
        {
            return cellReference;
        }

        return $"redacted-cell-{index:D3}";
    }

    private static DateTimeOffset BinTime(DateTimeOffset value, TimeSpan binSize)
    {
        long ticks = value.UtcTicks / binSize.Ticks * binSize.Ticks;
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private static string FormatTimeBinPolicy(TimeSpan binSize)
    {
        return FormattableString.Invariant($"utc-floor-{(int)binSize.TotalMinutes}-minutes");
    }
}

internal sealed record RetainedValue<T>(T Value, DateTimeOffset ExpiresAt);
