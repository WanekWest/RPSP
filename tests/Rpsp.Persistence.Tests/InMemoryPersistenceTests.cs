using System.Reflection;
using Rpsp.Core.Baselines;
using Rpsp.Core.Observations;
using Xunit;

namespace Rpsp.Persistence.Tests;

public sealed class InMemoryPersistenceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BaselineProfileStoreRoundTripsProfile()
    {
        var store = new InMemoryBaselineProfileStore();
        BaselineProfile profile = CreateBaselineProfile("profile-a");

        store.Save(profile, FixedNow.AddDays(7D));

        BaselineProfile? retrieved = store.FindByProfileId("profile-a");

        Assert.NotNull(retrieved);
        Assert.Equal(profile.ProfileId, retrieved.ProfileId);
        Assert.Single(retrieved.TrustedCells);
    }

    [Fact]
    public void TrustedSignatureStoreRoundTripsByProfile()
    {
        var store = new InMemoryTrustedCellSignatureStore();
        TrustedCellSignature signature = CreateTrustedSignature("trusted-a");

        store.Upsert("profile-a", signature, FixedNow.AddDays(7D));

        TrustedCellSignature retrieved = Assert.Single(store.GetByProfile("profile-a"));

        Assert.Equal(signature.SignatureId, retrieved.SignatureId);
        Assert.Equal(signature.NetworkCellPseudonym, retrieved.NetworkCellPseudonym);
    }

    [Fact]
    public void ObservationRingBufferRespectsMaxCount()
    {
        var buffer = new InMemoryObservationRingBuffer(maxCount: 2);

        buffer.Add(CreateObservationSummary("observation-1", FixedNow));
        buffer.Add(CreateObservationSummary("observation-2", FixedNow.AddSeconds(30D)));
        buffer.Add(CreateObservationSummary("observation-3", FixedNow.AddSeconds(60D)));

        IReadOnlyList<ObservationSummary> snapshot = buffer.Snapshot();

        Assert.Equal(2, snapshot.Count);
        Assert.Equal("observation-2", snapshot[0].ObservationId);
        Assert.Equal("observation-3", snapshot[1].ObservationId);
    }

    [Fact]
    public void RetentionPolicyRemovesExpiredRecordsUsingSuppliedTimestamp()
    {
        var profiles = new InMemoryBaselineProfileStore();
        var signatures = new InMemoryTrustedCellSignatureStore();
        var observations = new InMemoryObservationRingBuffer(maxCount: 8);
        var enforcer = new RetentionPolicyEnforcer();

        profiles.Save(CreateBaselineProfile("expired-profile"), FixedNow.AddMinutes(-1D));
        profiles.Save(CreateBaselineProfile("active-profile"), FixedNow.AddMinutes(30D));
        signatures.Upsert("profile-a", CreateTrustedSignature("expired-signature"), FixedNow.AddMinutes(-1D));
        observations.Add(CreateObservationSummary("expired-observation", FixedNow.AddMinutes(-5D), FixedNow.AddMinutes(-1D)));
        observations.Add(CreateObservationSummary("active-observation", FixedNow, FixedNow.AddMinutes(30D)));

        RetentionCleanupResult result = enforcer.Enforce(FixedNow, profiles, signatures, observations);

        Assert.Equal(3, result.StoreCount);
        Assert.Equal(3, result.RemovedRecordCount);
        Assert.Null(profiles.FindByProfileId("expired-profile"));
        Assert.NotNull(profiles.FindByProfileId("active-profile"));
        Assert.Empty(signatures.GetByProfile("profile-a"));
        Assert.Equal("active-observation", Assert.Single(observations.Snapshot()).ObservationId);
    }

    [Fact]
    public void PseudonymKeyProviderUsesInjectedTestDescriptor()
    {
        var descriptor = new PseudonymKeyDescriptor(
            "test-key-version-1",
            "local-baseline-test",
            IsTestOnly: true);
        var provider = new InMemoryPseudonymKeyProvider(descriptor);

        PseudonymKeyDescriptor current = provider.GetCurrentKey();

        Assert.True(provider.IsAvailable);
        Assert.True(current.IsTestOnly);
        Assert.Equal("test-key-version-1", current.KeyId);
        Assert.DoesNotContain("production", current.KeyId, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DebugExportRedactorRemovesRawAndSensitiveFields()
    {
        var redactor = new DebugExportRedactor();
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["safeScoreBand"] = "low",
            ["rawCellValue"] = "cell-value",
            ["routeSequence"] = "precise-path",
            ["accountAlias"] = "account-value",
            ["emptyOptional"] = null
        };

        RedactedDebugExport export = redactor.Redact(fields);

        Assert.Equal("low", export.Fields["safeScoreBand"]);
        Assert.DoesNotContain("rawCellValue", export.Fields.Keys);
        Assert.DoesNotContain("routeSequence", export.Fields.Keys);
        Assert.DoesNotContain("accountAlias", export.Fields.Keys);
        Assert.Contains("emptyOptional", export.RemovedFieldNames);
    }

    [Fact]
    public void InMemoryDebugExportPseudonymizesRawCellReferences()
    {
        var builder = new InMemoryDebugExportBuilder();
        ObservationSummary summary = CreateObservationSummary(
            "raw-local-observation",
            FixedNow.AddMinutes(7D),
            ["raw-cell-a", "cell-pseudonym:trusted-a", "raw-cell-a"]);

        InMemoryDebugExport export = builder.Build(
            [summary],
            new Dictionary<string, string?>(StringComparer.Ordinal),
            FixedNow);
        RedactedObservationExport observation = Assert.Single(export.Observations);

        Assert.Equal("observation-0000", observation.ExportObservationId);
        Assert.Contains("cell-pseudonym:trusted-a", observation.CellPseudonyms);
        Assert.Contains("redacted-cell-000", observation.CellPseudonyms);
        Assert.DoesNotContain("raw-cell-a", observation.CellPseudonyms);
    }

    [Fact]
    public void InMemoryDebugExportOmitsSensitiveMetadataFields()
    {
        var builder = new InMemoryDebugExportBuilder();
        var metadata = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["safeScoreBand"] = "low",
            ["rawRoute"] = "route-like-value",
            ["hardwareAlias"] = "hardware-value",
            ["accountAlias"] = "account-value"
        };

        InMemoryDebugExport export = builder.Build(
            [CreateObservationSummary("observation-a", FixedNow)],
            metadata,
            FixedNow);

        Assert.Equal("low", export.Metadata.Fields["safeScoreBand"]);
        Assert.DoesNotContain("rawRoute", export.Metadata.Fields.Keys);
        Assert.DoesNotContain("hardwareAlias", export.Metadata.Fields.Keys);
        Assert.DoesNotContain("accountAlias", export.Metadata.Fields.Keys);
    }

    [Fact]
    public void InMemoryDebugExportCoarsensObservationTime()
    {
        var builder = new InMemoryDebugExportBuilder(
            new DebugExportOptions(TimeSpan.FromMinutes(15D)));

        InMemoryDebugExport export = builder.Build(
            [CreateObservationSummary("observation-a", FixedNow.AddMinutes(14D).AddSeconds(59D))],
            new Dictionary<string, string?>(StringComparer.Ordinal),
            FixedNow);

        RedactedObservationExport observation = Assert.Single(export.Observations);
        Assert.Equal(FixedNow, observation.ObservedTimeBinStart);
        Assert.Equal("utc-floor-15-minutes", observation.TimeBinPolicy);
    }

    [Fact]
    public void InMemoryDebugExportManifestRecordsTransformVersion()
    {
        var builder = new InMemoryDebugExportBuilder(
            new DebugExportOptions(
                TimeSpan.FromMinutes(30D),
                ExportFormatVersion: "test-format-v1",
                PrivacyTransformVersion: "test-transform-v2"));

        InMemoryDebugExport export = builder.Build(
            [CreateObservationSummary("observation-a", FixedNow)],
            new Dictionary<string, string?>(StringComparer.Ordinal),
            FixedNow);

        Assert.Equal("test-format-v1", export.Manifest.ExportFormatVersion);
        Assert.Equal("test-transform-v2", export.Manifest.PrivacyTransformVersion);
        Assert.Equal("utc-floor-30-minutes", export.Manifest.TimeBinPolicy);
        Assert.Contains("raw-cell-fields", export.Manifest.ExcludedSensitiveClasses);
    }

    [Fact]
    public void InMemoryDebugExportContainsNoStableIdentityTerms()
    {
        var builder = new InMemoryDebugExportBuilder();
        var metadata = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["safeScoreBand"] = "low",
            ["phoneAlias"] = "redacted",
            [string.Concat("sub", "scriberAlias")] = "redacted"
        };

        InMemoryDebugExport export = builder.Build(
            [CreateObservationSummary("observation-a", FixedNow, ["cell-pseudonym:a"])],
            metadata,
            FixedNow);
        string serialized = string.Join(
            '|',
            export.Manifest.ExcludedSensitiveClasses
                .Concat(export.Metadata.Fields.Keys)
                .Concat(export.Metadata.Fields.Values)
                .Concat(export.Observations.SelectMany(static observation => observation.CellPseudonyms)));

        Assert.DoesNotContain(string.Concat("IM", "SI"), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(string.Concat("IM", "EI"), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(string.Concat("MS", "ISDN"), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(string.Concat("sub", "scriber"), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InMemoryDebugExportIsDeterministic()
    {
        var builder = new InMemoryDebugExportBuilder();
        ObservationSummary[] observations =
        [
            CreateObservationSummary("observation-a", FixedNow, ["cell-pseudonym:b", "cell-pseudonym:a"]),
            CreateObservationSummary("observation-b", FixedNow.AddMinutes(16D), ["raw-cell-b"])
        ];
        var metadata = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["safeScoreBand"] = "low",
            ["rawCellValue"] = "raw"
        };

        InMemoryDebugExport first = builder.Build(observations, metadata, FixedNow);
        InMemoryDebugExport second = builder.Build(observations, metadata, FixedNow);

        Assert.Equal(CreateExportSignature(first), CreateExportSignature(second));
    }

    [Fact]
    public void PublicPersistenceModelNamesAvoidSensitiveIdentityTerms()
    {
        string[] sensitiveTerms =
        [
            "Hardware",
            "PhoneNumber",
            "Account",
            "PermanentIdentity",
            "RawRoute"
        ];
        Type[] publicTypes = typeof(InMemoryBaselineProfileStore)
            .Assembly
            .GetExportedTypes()
            .Where(static type => type.Namespace == "Rpsp.Persistence")
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

    [Fact]
    public void PersistenceProjectHasNoFileDatabaseDependencies()
    {
        AssemblyName[] references = typeof(InMemoryBaselineProfileStore)
            .Assembly
            .GetReferencedAssemblies();
        string[] forbiddenReferences = ["Microsoft.Data.Sqlite", "System.Data.SqlClient", "Npgsql"];

        foreach (string forbiddenReference in forbiddenReferences)
        {
            Assert.DoesNotContain(
                references,
                reference => string.Equals(reference.Name, forbiddenReference, StringComparison.Ordinal));
        }
    }

    private static BaselineProfile CreateBaselineProfile(string profileId)
    {
        return new BaselineProfile(
            profileId,
            FixedNow.AddDays(-7D),
            FixedNow,
            [CreateTrustedSignature("trusted-a")]);
    }

    private static TrustedCellSignature CreateTrustedSignature(string signatureId)
    {
        return new TrustedCellSignature(
            signatureId,
            RadioAccessTechnology.Lte,
            PlmnPseudonym: "plmn-pseudonym",
            NetworkCellPseudonym: "cell-pseudonym");
    }

    private static ObservationSummary CreateObservationSummary(
        string observationId,
        DateTimeOffset observedAt,
        DateTimeOffset? expiresAt = null)
    {
        return CreateObservationSummary(
            observationId,
            observedAt,
            ["cell-pseudonym"],
            expiresAt);
    }

    private static ObservationSummary CreateObservationSummary(
        string observationId,
        DateTimeOffset observedAt,
        IReadOnlyList<string> cellPseudonyms,
        DateTimeOffset? expiresAt = null)
    {
        return new ObservationSummary(
            observationId,
            observedAt,
            cellPseudonyms,
            expiresAt ?? observedAt.AddMinutes(30D),
            ObservationSourceKind.Synthetic.ToString());
    }

    private static string CreateExportSignature(InMemoryDebugExport export)
    {
        string observations = string.Join(
            ';',
            export.Observations.Select(static observation =>
                string.Join(
                    ',',
                    observation.ExportObservationId,
                    observation.ObservedTimeBinStart.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    observation.TimeBinPolicy,
                    observation.SourceKind,
                    string.Join("/", observation.CellPseudonyms),
                    observation.RemovedCellReferenceCount.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        string metadata = string.Join(
            ';',
            export.Metadata.Fields
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => string.Concat(pair.Key, "=", pair.Value)));
        string removed = string.Join(
            ',',
            export.Metadata.RemovedFieldNames.Order(StringComparer.Ordinal));

        return string.Join(
            '|',
            export.Manifest.ExportFormatVersion,
            export.Manifest.PrivacyTransformVersion,
            export.Manifest.TimeBinPolicy,
            export.Manifest.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            string.Join(",", export.Manifest.IncludedRecordClasses),
            string.Join(",", export.Manifest.ExcludedSensitiveClasses),
            observations,
            metadata,
            removed);
    }
}
