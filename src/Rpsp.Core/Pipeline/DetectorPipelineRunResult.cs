using Rpsp.Core.Detectors;
using Rpsp.Core.Reporting;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Pipeline;

public sealed record DetectorPipelineRunResult(
    StationaryHandoverDetectionResult StationaryHandover,
    RatDowngradeDetectionResult RatDowngrade,
    UnknownStrongCellDetectionResult UnknownStrongCell,
    NeighborGraphDetectionResult NeighborGraph,
    IReadOnlyList<DetectorRiskContribution> Contributions,
    IReadOnlyList<DetectorReport> DetectorReports);
