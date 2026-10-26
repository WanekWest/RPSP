using Rpsp.Core.Baselines;
using Rpsp.Core.Observations;
using Rpsp.Core.Reporting;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Pipeline;

public sealed record RpspDetectionPipelineResult(
    IReadOnlyList<RadioObservation> Observations,
    BaselineProfile Baseline,
    IReadOnlyList<DetectorRiskContribution> DetectorContributions,
    CompositeRiskAssessment CompositeAssessment,
    DetectorReport CompositeReport,
    IReadOnlyList<DetectorReport> DetectorReports);
