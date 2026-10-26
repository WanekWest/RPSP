using Rpsp.Core.Detectors;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Pipeline;

public sealed record RpspDetectionPipelineOptions
{
    public StationaryHandoverDetectorOptions? StationaryHandoverOptions { get; init; }

    public RatDowngradeDetectorOptions? RatDowngradeOptions { get; init; }

    public UnknownStrongCellDetectorOptions? UnknownStrongCellOptions { get; init; }

    public NeighborGraphDetectorOptions? NeighborGraphOptions { get; init; }

    public CompositeRiskScorerOptions? CompositeRiskScorerOptions { get; init; }
}
