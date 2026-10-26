namespace Rpsp.Evaluation.Experiments;

public sealed record RunProvenance(
    string GitCommit,
    string DotnetSdkVersion,
    string RuntimeVersion,
    string ConfigurationHash,
    string TimingMode);

public sealed record ExperimentResultRecord(
    string SchemaVersion,
    string ExperimentId,
    string RunId,
    string GitCommit,
    string DotnetSdkVersion,
    string RuntimeVersion,
    string ConfigurationHash,
    string SourceKind,
    string SourceName,
    string WindowName,
    string AblationProfile,
    double BoundaryScale,
    int Replicate,
    int Seed,
    string ConstructedLabel,
    string DetectorFamily,
    double RiskScore,
    string ScoreBand,
    string WarningDecision,
    string SuppressionState,
    string SuppressionReason,
    string EvidenceLevel,
    string DataSource,
    int ObservationCount,
    double ElapsedMilliseconds,
    string TimingMode);

public sealed record ExperimentResultSet(
    ExperimentManifest Manifest,
    RunProvenance Provenance,
    IReadOnlyList<ExperimentResultRecord> Records);
