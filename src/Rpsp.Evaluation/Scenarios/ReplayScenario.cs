using Rpsp.Core.Baselines;
using Rpsp.Core.Scoring;

namespace Rpsp.Evaluation.Scenarios;

public sealed record ReplayScenario(
    ReplayScenarioKind Kind,
    string Name,
    string Description,
    ReplayTrace Trace,
    bool ExpectedStationaryHandoverSuppression,
    double? MinimumStationaryHandoverScore = null,
    double? MaximumStationaryHandoverScore = null,
    RiskSeverity? ExpectedStationaryHandoverSeverity = null,
    bool HasStationaryHandoverExpectation = true,
    bool HasRatDowngradeExpectation = false,
    bool ExpectedRatDowngradeSuppression = false,
    double? MinimumRatDowngradeScore = null,
    double? MaximumRatDowngradeScore = null,
    RiskSeverity? ExpectedRatDowngradeSeverity = null,
    BaselineProfile? Baseline = null,
    bool HasUnknownStrongCellExpectation = false,
    bool ExpectedUnknownStrongCellSuppression = false,
    double? MinimumUnknownStrongCellScore = null,
    double? MaximumUnknownStrongCellScore = null,
    RiskSeverity? ExpectedUnknownStrongCellSeverity = null,
    bool HasNeighborGraphExpectation = false,
    bool ExpectedNeighborGraphSuppression = false,
    double? MinimumNeighborGraphScore = null,
    double? MaximumNeighborGraphScore = null,
    RiskSeverity? ExpectedNeighborGraphSeverity = null);
