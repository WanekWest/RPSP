namespace Rpsp.Evaluation.Stress;

public enum SyntheticStressTraceKind
{
    LongStableUrbanBaseline = 0,
    LongBenignUrbanMobility = 1,
    IndoorFadingStress = 2,
    BorderOscillationStress = 3,
    MixedSuspiciousBurst = 4,
    RepeatedUnknownStrongCellStress = 5,
    RepeatedNeighborGraphDriftStress = 6,
    NoisyUnavailableMetricsStress = 7,
    ColdStartThenWarmupStress = 8,
    CompositeMultiDetectorCorrelationStress = 9,
    RatNoServiceLegacyDwellStress = 10,
    MaintenanceOutageRecoveryStress = 11,
    MotionUnknownMixedChurnSuppressionStress = 12,
    UnknownStrongNeighborOnlyLongStress = 13,
    CompositeLowScoreAccumulationStress = 14,
    BaselineAgingDriftStress = 15,
    RoamingLikeContextStress = 16,
    TemporaryOperatorCellDeploymentStress = 17,
    MetricUnavailableWithChurnStress = 18,
    NearThresholdBorderOscillationStress = 19,
    LongMixedUrbanDayStress = 20,
    SparseEvidenceAmbiguousStress = 21,
    AlternatingBenignAndSuspiciousWindowsStress = 22,
    BaselineWarmupThenUnknownCellStress = 23,
    NeighborGraphPartialRecoveryStress = 24
}
