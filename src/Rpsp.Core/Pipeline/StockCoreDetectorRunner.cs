using Rpsp.Core.Baselines;
using Rpsp.Core.Detectors;
using Rpsp.Core.Observations;

namespace Rpsp.Core.Pipeline;

public sealed class StockCoreDetectorRunner : IRpspDetectorRunner
{
    private readonly NeighborGraphAnomalyDetector _neighborGraph;
    private readonly RatDowngradeRiskDetector _ratDowngrade;
    private readonly StationaryHandoverAnomalyDetector _stationaryHandover;
    private readonly UnknownStrongCellDetector _unknownStrongCell;

    public StockCoreDetectorRunner(RpspDetectionPipelineOptions? options = null)
    {
        RpspDetectionPipelineOptions pipelineOptions = options ?? new RpspDetectionPipelineOptions();

        _stationaryHandover = new StationaryHandoverAnomalyDetector(
            pipelineOptions.StationaryHandoverOptions);
        _ratDowngrade = new RatDowngradeRiskDetector(pipelineOptions.RatDowngradeOptions);
        _unknownStrongCell = new UnknownStrongCellDetector(pipelineOptions.UnknownStrongCellOptions);
        _neighborGraph = new NeighborGraphAnomalyDetector(pipelineOptions.NeighborGraphOptions);
    }

    public DetectorPipelineRunResult Run(
        IReadOnlyList<RadioObservation> observations,
        BaselineProfile baseline)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(baseline);

        StationaryHandoverDetectionResult stationaryHandover = _stationaryHandover.Analyze(observations);
        RatDowngradeDetectionResult ratDowngrade = _ratDowngrade.Analyze(observations);
        UnknownStrongCellDetectionResult unknownStrongCell = _unknownStrongCell.Analyze(observations, baseline);
        NeighborGraphDetectionResult neighborGraph = _neighborGraph.Analyze(observations, baseline);

        return new DetectorPipelineRunResult(
            stationaryHandover,
            ratDowngrade,
            unknownStrongCell,
            neighborGraph,
            DetectorPipelineContributionFactory.CreateContributions(
                stationaryHandover,
                ratDowngrade,
                unknownStrongCell,
                neighborGraph),
            DetectorPipelineContributionFactory.CreateDetectorReports(
                stationaryHandover,
                ratDowngrade,
                unknownStrongCell,
                neighborGraph));
    }
}
