namespace Rpsp.Evaluation.Stress;

public sealed record SyntheticStressExpectation(
    double? MaximumStationaryHandoverScore = null,
    double? MinimumStationaryHandoverScore = null,
    double? MaximumRatDowngradeScore = null,
    double? MinimumRatDowngradeScore = null,
    double? MaximumUnknownStrongCellScore = null,
    double? MinimumUnknownStrongCellScore = null,
    double? MaximumNeighborGraphScore = null,
    double? MinimumNeighborGraphScore = null,
    double? MaximumCompositeScore = null,
    double? MinimumCompositeScore = null,
    bool ExpectsEarlyColdStartSuppression = false,
    string? FocusWindowName = null);
