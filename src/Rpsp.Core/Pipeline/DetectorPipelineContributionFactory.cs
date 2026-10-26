using Rpsp.Core.Detectors;
using Rpsp.Core.Reporting;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Pipeline;

public static class DetectorPipelineContributionFactory
{
    public static IReadOnlyList<DetectorRiskContribution> CreateContributions(
        StationaryHandoverDetectionResult stationaryHandover,
        RatDowngradeDetectionResult ratDowngrade,
        UnknownStrongCellDetectionResult unknownStrongCell,
        NeighborGraphDetectionResult neighborGraph)
    {
        ArgumentNullException.ThrowIfNull(stationaryHandover);
        ArgumentNullException.ThrowIfNull(ratDowngrade);
        ArgumentNullException.ThrowIfNull(unknownStrongCell);
        ArgumentNullException.ThrowIfNull(neighborGraph);

        return
        [
            DetectorRiskContribution.FromStationaryHandover(stationaryHandover),
            DetectorRiskContribution.FromRatDowngrade(ratDowngrade),
            DetectorRiskContribution.FromUnknownStrongCell(unknownStrongCell),
            DetectorRiskContribution.FromNeighborGraph(neighborGraph)
        ];
    }

    public static IReadOnlyList<DetectorReport> CreateDetectorReports(
        StationaryHandoverDetectionResult stationaryHandover,
        RatDowngradeDetectionResult ratDowngrade,
        UnknownStrongCellDetectionResult unknownStrongCell,
        NeighborGraphDetectionResult neighborGraph)
    {
        ArgumentNullException.ThrowIfNull(stationaryHandover);
        ArgumentNullException.ThrowIfNull(ratDowngrade);
        ArgumentNullException.ThrowIfNull(unknownStrongCell);
        ArgumentNullException.ThrowIfNull(neighborGraph);

        return
        [
            StationaryHandoverReportFactory.Create(stationaryHandover),
            RatDowngradeReportFactory.Create(ratDowngrade),
            UnknownStrongCellReportFactory.Create(unknownStrongCell),
            NeighborGraphReportFactory.Create(neighborGraph)
        ];
    }
}
