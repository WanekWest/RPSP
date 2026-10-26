using System.Globalization;
using Rpsp.Core.Baselines;
using Rpsp.Core.Observations;

namespace Rpsp.Evaluation.Stress;

public static class SyntheticStressTraceCatalog
{
    private static readonly string[] StableNeighborGraph =
    [
        "synthetic-stress-neighbor-a",
        "synthetic-stress-neighbor-b",
        "synthetic-stress-neighbor-c"
    ];

    private static readonly string[] SmallDriftNeighborGraph =
    [
        "synthetic-stress-neighbor-a",
        "synthetic-stress-neighbor-b",
        "synthetic-stress-neighbor-d"
    ];

    private static readonly string[] SevereNeighborGraph =
    [
        "synthetic-stress-neighbor-x",
        "synthetic-stress-neighbor-y",
        "synthetic-stress-neighbor-z"
    ];

    private static readonly string[] AlternateSevereNeighborGraph =
    [
        "synthetic-stress-neighbor-p",
        "synthetic-stress-neighbor-q",
        "synthetic-stress-neighbor-r"
    ];

    private static readonly string[] StableGraphWithUnknownStrongNeighbor =
    [
        "synthetic-stress-neighbor-a",
        "synthetic-stress-neighbor-b",
        "synthetic-stress-neighbor-c",
        "synthetic-stress-unknown-strong-neighbor"
    ];

    public static IReadOnlyList<SyntheticStressTrace> All { get; } =
    [
        CreateLongStableUrbanBaseline(),
        CreateLongBenignUrbanMobility(),
        CreateIndoorFadingStress(),
        CreateBorderOscillationStress(),
        CreateMixedSuspiciousBurst(),
        CreateRepeatedUnknownStrongCellStress(),
        CreateRepeatedNeighborGraphDriftStress(),
        CreateNoisyUnavailableMetricsStress(),
        CreateColdStartThenWarmupStress(),
        CreateCompositeMultiDetectorCorrelationStress(),
        CreateRatNoServiceLegacyDwellStress(),
        CreateMaintenanceOutageRecoveryStress(),
        CreateMotionUnknownMixedChurnSuppressionStress(),
        CreateUnknownStrongNeighborOnlyLongStress(),
        CreateCompositeLowScoreAccumulationStress(),
        CreateBaselineAgingDriftStress(),
        CreateRoamingLikeContextStress(),
        CreateTemporaryOperatorCellDeploymentStress(),
        CreateMetricUnavailableWithChurnStress(),
        CreateNearThresholdBorderOscillationStress(),
        CreateLongMixedUrbanDayStress(),
        CreateSparseEvidenceAmbiguousStress(),
        CreateAlternatingBenignAndSuspiciousWindowsStress(),
        CreateBaselineWarmupThenUnknownCellStress(),
        CreateNeighborGraphPartialRecoveryStress()
    ];

    public static SyntheticStressTrace Get(SyntheticStressTraceKind kind)
    {
        return All.Single(trace => trace.Kind == kind);
    }

    private static SyntheticStressTrace CreateLongStableUrbanBaseline()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.LongStableUrbanBaseline;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 24; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-serving-a",
                StableNeighborGraph,
                servingRsrpDbm: -82D + (index % 3),
                neighborRsrpDbm: -88D + (index % 2));
        }

        return CreateTrace(
            kind,
            "Long stable urban baseline",
            "Long stationary baseline with stable serving cell, stable neighbor graph and light signal jitter.",
            builder.Build(),
            CreateBaseline(
                "stress-long-stable-urban-baseline",
                ["synthetic-stress-serving-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumStationaryHandoverScore: 0.2D,
                MaximumRatDowngradeScore: 0.2D,
                MaximumUnknownStrongCellScore: 0.2D,
                MaximumNeighborGraphScore: 0.2D,
                MaximumCompositeScore: 0.25D));
    }

    private static SyntheticStressTrace CreateLongBenignUrbanMobility()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.LongBenignUrbanMobility;
        var builder = new SyntheticStressTraceBuilder(kind);
        string[] servingCells =
        [
            "synthetic-stress-mobile-a",
            "synthetic-stress-mobile-b",
            "synthetic-stress-mobile-c",
            "synthetic-stress-mobile-d",
            "synthetic-stress-mobile-e",
            "synthetic-stress-mobile-f"
        ];

        for (int index = 0; index < 24; index++)
        {
            string servingCell = servingCells[index % servingCells.Length];
            string[] neighbors = index % 2 == 0 ? StableNeighborGraph : SmallDriftNeighborGraph;
            builder.AddServingCellWithNeighbors(
                Offset(index),
                index % 3 == 0 ? DeviceMotionState.InVehicle : DeviceMotionState.Moving,
                servingCell,
                neighbors,
                servingRsrpDbm: -84D + (index % 4),
                neighborRsrpDbm: -90D);
        }

        return CreateTrace(
            kind,
            "Long benign urban mobility",
            "Long moving trace with many serving-cell transitions and normal neighbor variation.",
            builder.Build(),
            CreateBaseline("stress-long-benign-urban-mobility", [.. servingCells, .. StableNeighborGraph, .. SmallDriftNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumStationaryHandoverScore: 0.3D,
                MaximumNeighborGraphScore: 0.3D,
                MaximumCompositeScore: 0.5D));
    }

    private static SyntheticStressTrace CreateIndoorFadingStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.IndoorFadingStress;
        var builder = new SyntheticStressTraceBuilder(kind);
        double[] rsrpPattern =
        [
            -79D,
            -88D,
            -99D,
            -111D,
            -104D,
            -91D,
            -82D,
            -78D
        ];

        for (int index = 0; index < 24; index++)
        {
            RadioAccessTechnology rat = index == 10 ? RadioAccessTechnology.Umts : RadioAccessTechnology.Lte;
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-indoor-serving",
                StableNeighborGraph,
                servingRsrpDbm: rsrpPattern[index % rsrpPattern.Length],
                neighborRsrpDbm: -91D,
                radioAccessTechnology: rat);
        }

        return CreateTrace(
            kind,
            "Indoor fading stress",
            "Long stationary signal degradation and recovery cycles with stable serving context and short benign fallback.",
            builder.Build(),
            CreateBaseline("stress-indoor-fading", ["synthetic-stress-indoor-serving", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumStationaryHandoverScore: 0.3D,
                MaximumRatDowngradeScore: 0.79D,
                MaximumCompositeScore: 0.79D));
    }

    private static SyntheticStressTrace CreateBorderOscillationStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.BorderOscillationStress;
        var builder = new SyntheticStressTraceBuilder(kind);
        string[] servingSequence =
        [
            "synthetic-stress-border-a",
            "synthetic-stress-border-b",
            "synthetic-stress-border-a",
            "synthetic-stress-border-b",
            "synthetic-stress-border-a"
        ];

        for (int index = 0; index < 24; index++)
        {
            string servingCell = index < servingSequence.Length
                ? servingSequence[index]
                : "synthetic-stress-border-a";
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Walking,
                servingCell,
                StableNeighborGraph,
                servingRsrpDbm: -86D + (index % 3),
                neighborRsrpDbm: -89D);
        }

        return CreateTrace(
            kind,
            "Border oscillation stress",
            "Low-mobility edge oscillation with limited distinct serving cells and stable neighbor graph.",
            builder.Build(),
            CreateBaseline("stress-border-oscillation", ["synthetic-stress-border-a", "synthetic-stress-border-b", .. StableNeighborGraph]),
            new SyntheticStressExpectation(MaximumCompositeScore: 0.79D));
    }

    private static SyntheticStressTrace CreateMixedSuspiciousBurst()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.MixedSuspiciousBurst;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 6; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-serving-a",
                StableNeighborGraph,
                servingRsrpDbm: -83D,
                neighborRsrpDbm: -88D);
        }

        string[] burstServing =
        [
            "synthetic-stress-serving-b",
            "synthetic-stress-unknown-burst",
            "synthetic-stress-serving-c",
            "synthetic-stress-unknown-burst",
            "synthetic-stress-serving-d",
            "synthetic-stress-unknown-burst",
            "synthetic-stress-serving-b",
            "synthetic-stress-unknown-burst"
        ];

        for (int index = 0; index < burstServing.Length; index++)
        {
            RadioAccessTechnology rat = index < 2 ? RadioAccessTechnology.Lte : RadioAccessTechnology.Umts;
            builder.AddServingCellWithNeighbors(
                Offset(6 + index),
                DeviceMotionState.Stationary,
                burstServing[index],
                index % 2 == 0 ? SevereNeighborGraph : AlternateSevereNeighborGraph,
                servingRsrpDbm: burstServing[index] == "synthetic-stress-unknown-burst" ? -63D : -78D,
                neighborRsrpDbm: -87D,
                radioAccessTechnology: rat);
        }

        for (int index = 14; index < 24; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-serving-a",
                StableNeighborGraph,
                servingRsrpDbm: -82D,
                neighborRsrpDbm: -88D);
        }

        return CreateTrace(
            kind,
            "Mixed suspicious burst",
            "Long trace with low-risk context before and after a short multi-detector suspicious burst.",
            builder.Build(),
            CreateBaseline("stress-mixed-suspicious-burst", ["synthetic-stress-serving-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(MinimumCompositeScore: 0.8D, FocusWindowName: "burst"),
            [
                new SyntheticStressWindow("pre-burst", 0, 6),
                new SyntheticStressWindow("burst", 6, 8),
                new SyntheticStressWindow("post-burst", 14, 6)
            ]);
    }

    private static SyntheticStressTrace CreateRepeatedUnknownStrongCellStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.RepeatedUnknownStrongCellStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 6; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-known-a",
                StableNeighborGraph,
                servingRsrpDbm: -83D);
        }

        for (int index = 6; index < 16; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-unknown-very-strong",
                StableNeighborGraph,
                servingRsrpDbm: -62D,
                neighborRsrpDbm: -88D);
        }

        return CreateTrace(
            kind,
            "Repeated unknown strong cell stress",
            "Repeated very strong unknown serving cell appears after sufficient trusted baseline context.",
            builder.Build(),
            CreateBaseline("stress-repeated-unknown-strong-cell", ["synthetic-stress-known-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MinimumUnknownStrongCellScore: 0.8D,
                MinimumCompositeScore: 0.8D));
    }

    private static SyntheticStressTrace CreateRepeatedNeighborGraphDriftStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.RepeatedNeighborGraphDriftStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 18; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-serving-a",
                index < 4 ? StableNeighborGraph : SevereNeighborGraph,
                servingRsrpDbm: -82D,
                neighborRsrpDbm: -88D);
        }

        return CreateTrace(
            kind,
            "Repeated neighbor graph drift stress",
            "Stationary trace with repeated low-similarity neighbor graph observations.",
            builder.Build(),
            CreateBaseline("stress-repeated-neighbor-graph-drift", StableNeighborGraph),
            new SyntheticStressExpectation(
                MinimumNeighborGraphScore: 0.8D,
                MinimumCompositeScore: 0.8D));
    }

    private static SyntheticStressTrace CreateNoisyUnavailableMetricsStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.NoisyUnavailableMetricsStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 18; index++)
        {
            if (index % 6 == 2)
            {
                builder.AddUnknownRatObservation(Offset(index), DeviceMotionState.Stationary);
            }
            else if (index % 6 == 4)
            {
                builder.AddNeighborOnly(
                    Offset(index),
                    DeviceMotionState.Stationary,
                    ["synthetic-stress-neighbor-a"],
                    neighborRsrpDbm: null);
            }
            else
            {
                builder.AddServingCell(
                    Offset(index),
                    DeviceMotionState.Stationary,
                    "synthetic-stress-serving-a",
                    servingRsrpDbm: index % 3 == 0 ? null : -84D);
            }
        }

        return CreateTrace(
            kind,
            "Noisy unavailable metrics stress",
            "Trace with unavailable signal metrics, missing neighbors and unknown RAT values.",
            builder.Build(),
            CreateBaseline("stress-noisy-unavailable-metrics", ["synthetic-stress-serving-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumStationaryHandoverScore: 0.3D,
                MaximumRatDowngradeScore: 0.3D,
                MaximumUnknownStrongCellScore: 0.3D,
                MaximumNeighborGraphScore: 0.3D,
                MaximumCompositeScore: 0.35D));
    }

    private static SyntheticStressTrace CreateColdStartThenWarmupStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.ColdStartThenWarmupStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 3; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-warmup-a",
                StableNeighborGraph,
                servingRsrpDbm: -83D);
        }

        for (int index = 3; index < 16; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-warmup-a",
                StableNeighborGraph,
                servingRsrpDbm: -82D + (index % 2),
                neighborRsrpDbm: -88D);
        }

        return CreateTrace(
            kind,
            "Cold start then warmup stress",
            "Trace starts below detector minimums and then warms into a stable trusted baseline context.",
            builder.Build(),
            CreateBaseline("stress-cold-start-then-warmup", ["synthetic-stress-warmup-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumCompositeScore: 0.25D,
                ExpectsEarlyColdStartSuppression: true,
                FocusWindowName: "early-cold-start"),
            [
                new SyntheticStressWindow("early-cold-start", 0, 3),
                new SyntheticStressWindow("warm-baseline", 3, 8)
            ]);
    }

    private static SyntheticStressTrace CreateCompositeMultiDetectorCorrelationStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.CompositeMultiDetectorCorrelationStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 5; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-correlation-a",
                StableNeighborGraph,
                servingRsrpDbm: -82D);
        }

        string[] correlationServing =
        [
            "synthetic-stress-correlation-b",
            "synthetic-stress-correlation-c",
            "synthetic-stress-unknown-correlation",
            "synthetic-stress-correlation-d",
            "synthetic-stress-unknown-correlation",
            "synthetic-stress-correlation-b",
            "synthetic-stress-unknown-correlation",
            "synthetic-stress-correlation-c"
        ];

        for (int index = 0; index < correlationServing.Length; index++)
        {
            RadioAccessTechnology rat = index < 2 ? RadioAccessTechnology.Lte : RadioAccessTechnology.Gsm;
            builder.AddServingCellWithNeighbors(
                Offset(5 + index),
                DeviceMotionState.Stationary,
                correlationServing[index],
                index % 2 == 0 ? SevereNeighborGraph : AlternateSevereNeighborGraph,
                servingRsrpDbm: correlationServing[index] == "synthetic-stress-unknown-correlation" ? -62D : -78D,
                neighborRsrpDbm: -87D,
                radioAccessTechnology: rat);
        }

        for (int index = 13; index < 24; index++)
        {
            string serving = index % 4 == 0
                ? "synthetic-stress-correlation-rat"
                : "synthetic-stress-correlation-a";
            RadioAccessTechnology rat = index % 4 == 0 ? RadioAccessTechnology.Umts : RadioAccessTechnology.Lte;
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                serving,
                index % 5 == 0 ? SevereNeighborGraph : StableNeighborGraph,
                servingRsrpDbm: -82D,
                neighborRsrpDbm: -88D,
                radioAccessTechnology: rat);
        }

        return CreateTrace(
            kind,
            "Composite multi-detector correlation stress",
            "Several detector families become active in bounded windows while composite boost remains clamped.",
            builder.Build(),
            CreateBaseline("stress-composite-multi-detector-correlation", ["synthetic-stress-correlation-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MinimumCompositeScore: 0.8D,
                MaximumCompositeScore: 1D,
                FocusWindowName: "correlated-burst"),
            [
                new SyntheticStressWindow("correlated-burst", 5, 8)
            ]);
    }

    private static SyntheticStressTrace CreateRatNoServiceLegacyDwellStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.RatNoServiceLegacyDwellStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 6; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-rat-modern-a",
                StableNeighborGraph,
                servingRsrpDbm: -82D,
                neighborRsrpDbm: -88D,
                radioAccessTechnology: RadioAccessTechnology.Lte);
        }

        for (int index = 6; index < 9; index++)
        {
            builder.AddServiceUnavailable(Offset(index), DeviceMotionState.Stationary);
        }

        for (int index = 9; index < 17; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-rat-legacy-dwell",
                StableNeighborGraph,
                servingRsrpDbm: -84D,
                neighborRsrpDbm: -89D,
                radioAccessTechnology: RadioAccessTechnology.Umts);
        }

        for (int index = 17; index < 24; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-rat-modern-a",
                StableNeighborGraph,
                servingRsrpDbm: -82D,
                neighborRsrpDbm: -88D,
                radioAccessTechnology: RadioAccessTechnology.Lte);
        }

        return CreateTrace(
            kind,
            "RAT no-service legacy dwell stress",
            "Stationary modern history, bounded service-unavailable gap and sustained legacy RAT dwell before modern recovery.",
            builder.Build(),
            CreateBaseline("stress-rat-no-service-legacy-dwell", ["synthetic-stress-rat-modern-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MinimumRatDowngradeScore: 0.8D,
                MinimumCompositeScore: 0.8D,
                FocusWindowName: "legacy-dwell"),
            [
                new SyntheticStressWindow("pre-outage", 0, 6),
                new SyntheticStressWindow("legacy-dwell", 3, 14),
                new SyntheticStressWindow("post-recovery", 17, 7)
            ]);
    }

    private static SyntheticStressTrace CreateMaintenanceOutageRecoveryStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.MaintenanceOutageRecoveryStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 6; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-maintenance-modern-a",
                StableNeighborGraph,
                servingRsrpDbm: -82D,
                neighborRsrpDbm: -88D,
                radioAccessTechnology: RadioAccessTechnology.Lte);
        }

        for (int index = 6; index < 10; index++)
        {
            builder.AddServiceUnavailable(Offset(index), DeviceMotionState.Stationary);
        }

        for (int index = 10; index < 22; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-maintenance-modern-a",
                StableNeighborGraph,
                servingRsrpDbm: -83D + (index % 2),
                neighborRsrpDbm: -89D,
                radioAccessTechnology: RadioAccessTechnology.Lte);
        }

        return CreateTrace(
            kind,
            "Maintenance outage recovery stress",
            "Service-unavailable gap followed by modern RAT recovery without sustained legacy dwell.",
            builder.Build(),
            CreateBaseline("stress-maintenance-outage-recovery", ["synthetic-stress-maintenance-modern-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumRatDowngradeScore: 0.2D,
                MaximumCompositeScore: 0.35D,
                FocusWindowName: "modern-recovery"),
            [
                new SyntheticStressWindow("outage-gap", 4, 8),
                new SyntheticStressWindow("modern-recovery", 10, 8)
            ]);
    }

    private static SyntheticStressTrace CreateMotionUnknownMixedChurnSuppressionStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.MotionUnknownMixedChurnSuppressionStress;
        var builder = new SyntheticStressTraceBuilder(kind);
        string[] servingSequence =
        [
            "synthetic-stress-motion-known-a",
            "synthetic-stress-motion-known-b",
            "synthetic-stress-motion-unknown-strong",
            "synthetic-stress-motion-known-c",
            "synthetic-stress-motion-unknown-strong",
            "synthetic-stress-motion-known-b",
            "synthetic-stress-motion-unknown-strong",
            "synthetic-stress-motion-known-a"
        ];

        for (int index = 0; index < 4; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Unknown,
                "synthetic-stress-motion-known-a",
                StableNeighborGraph,
                servingRsrpDbm: -82D,
                neighborRsrpDbm: -88D);
        }

        for (int index = 0; index < servingSequence.Length; index++)
        {
            string servingCell = servingSequence[index];
            RadioAccessTechnology rat = index < 2 ? RadioAccessTechnology.Lte : RadioAccessTechnology.Umts;
            builder.AddServingCellWithNeighbors(
                Offset(4 + index),
                DeviceMotionState.Unknown,
                servingCell,
                index % 2 == 0 ? SevereNeighborGraph : AlternateSevereNeighborGraph,
                servingRsrpDbm: servingCell == "synthetic-stress-motion-unknown-strong" ? -62D : -78D,
                neighborRsrpDbm: -87D,
                radioAccessTechnology: rat);
        }

        return CreateTrace(
            kind,
            "Motion unknown mixed churn suppression stress",
            "Unknown motion context with churn-like, legacy-RAT and neighbor-graph noise that must remain suppressed or bounded.",
            builder.Build(),
            CreateBaseline("stress-motion-unknown-mixed-churn", ["synthetic-stress-motion-known-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumStationaryHandoverScore: 0.2D,
                MaximumRatDowngradeScore: 0.2D,
                MaximumUnknownStrongCellScore: 0.2D,
                MaximumNeighborGraphScore: 0.2D,
                MaximumCompositeScore: 0.2D,
                FocusWindowName: "unknown-motion-churn"),
            [
                new SyntheticStressWindow("unknown-motion-churn", 4, 8)
            ]);
    }

    private static SyntheticStressTrace CreateUnknownStrongNeighborOnlyLongStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.UnknownStrongNeighborOnlyLongStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 28; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-neighbor-only-serving",
                StableGraphWithUnknownStrongNeighbor,
                servingRsrpDbm: -82D + (index % 2),
                neighborRsrpDbm: -62D);
        }

        return CreateTrace(
            kind,
            "Unknown strong neighbor-only long stress",
            "Long stable serving trace with repeated strong unknown neighbor-only visibility and no unknown serving appearances.",
            builder.Build(),
            CreateBaseline("stress-unknown-strong-neighbor-only-long", ["synthetic-stress-neighbor-only-serving", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumUnknownStrongCellScore: 0.56D,
                MaximumNeighborGraphScore: 0.3D,
                MaximumCompositeScore: 0.6D));
    }

    private static SyntheticStressTrace CreateCompositeLowScoreAccumulationStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.CompositeLowScoreAccumulationStress;
        var builder = new SyntheticStressTraceBuilder(kind);
        string[] servingSequence =
        [
            "synthetic-stress-low-score-serving-a",
            "synthetic-stress-low-score-serving-b",
            "synthetic-stress-low-score-serving-c",
            "synthetic-stress-low-score-serving-a"
        ];

        for (int index = 0; index < 4; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                servingSequence[index],
                StableGraphWithUnknownStrongNeighbor,
                servingRsrpDbm: -82D,
                neighborRsrpDbm: -62D);
        }

        for (int index = 4; index < 14; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-low-score-serving-a",
                StableNeighborGraph,
                servingRsrpDbm: -83D + (index % 2),
                neighborRsrpDbm: -88D);
        }

        return CreateTrace(
            kind,
            "Composite low-score accumulation stress",
            "Short bounded serving churn plus low neighbor-only unknown-cell evidence to verify low and suppressed contributions do not accumulate into high composite risk.",
            builder.Build(),
            CreateBaseline("stress-composite-low-score-accumulation", StableNeighborGraph),
            new SyntheticStressExpectation(
                MaximumStationaryHandoverScore: 0.79D,
                MaximumUnknownStrongCellScore: 0.56D,
                MaximumCompositeScore: 0.79D,
                FocusWindowName: "low-score-mix"),
            [
                new SyntheticStressWindow("low-score-mix", 0, 6)
            ]);
    }

    private static SyntheticStressTrace CreateBaselineAgingDriftStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.BaselineAgingDriftStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 8; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-aging-known-a",
                index < 4 ? StableNeighborGraph : SmallDriftNeighborGraph,
                servingRsrpDbm: -83D + (index % 2),
                neighborRsrpDbm: -89D);
        }

        return CreateTrace(
            kind,
            "Baseline aging drift stress",
            "Aged trusted baseline with small bounded neighbor drift that should remain below high composite risk.",
            builder.Build(),
            CreateBaseline(
                "stress-baseline-aging-drift",
                ["synthetic-stress-aging-known-a", .. StableNeighborGraph, "synthetic-stress-neighbor-d"]),
            new SyntheticStressExpectation(
                MaximumUnknownStrongCellScore: 0.2D,
                MaximumCompositeScore: 0.6D,
                FocusWindowName: "aged-drift"),
            [
                new SyntheticStressWindow("aged-drift", 0, 8)
            ]);
    }

    private static SyntheticStressTrace CreateRoamingLikeContextStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.RoamingLikeContextStress;
        var builder = new SyntheticStressTraceBuilder(kind);
        string[] visitedCells =
        [
            "synthetic-stress-visited-a",
            "synthetic-stress-visited-b",
            "synthetic-stress-visited-c",
            "synthetic-stress-visited-d"
        ];

        for (int index = 0; index < 12; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.InVehicle,
                visitedCells[index % visitedCells.Length],
                index % 2 == 0 ? SevereNeighborGraph : AlternateSevereNeighborGraph,
                servingRsrpDbm: -76D + (index % 2),
                neighborRsrpDbm: -84D);
        }

        return CreateTrace(
            kind,
            "Roaming-like context stress",
            "Visited-context-like mobility and baseline mismatch modeled as ambiguous local evidence rather than proof.",
            builder.Build(),
            CreateBaseline("stress-roaming-like-context", ["synthetic-stress-home-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumStationaryHandoverScore: 0.2D,
                MaximumUnknownStrongCellScore: 0.2D,
                MaximumNeighborGraphScore: 0.2D,
                MaximumCompositeScore: 0.35D,
                FocusWindowName: "visited-context"),
            [
                new SyntheticStressWindow("visited-context", 0, 12)
            ]);
    }

    private static SyntheticStressTrace CreateTemporaryOperatorCellDeploymentStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.TemporaryOperatorCellDeploymentStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 5; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-temporary-known-a",
                StableNeighborGraph,
                servingRsrpDbm: -84D,
                neighborRsrpDbm: -90D);
        }

        for (int index = 5; index < 7; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-temporary-new-a",
                SmallDriftNeighborGraph,
                servingRsrpDbm: -82D,
                neighborRsrpDbm: -88D);
        }

        for (int index = 7; index < 12; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-temporary-known-a",
                StableNeighborGraph,
                servingRsrpDbm: -84D,
                neighborRsrpDbm: -90D);
        }

        return CreateTrace(
            kind,
            "Temporary operator cell deployment stress",
            "Short-lived local deployment-like serving cell appearance that remains bounded without baseline refresh.",
            builder.Build(),
            CreateBaseline("stress-temporary-operator-cell-deployment", ["synthetic-stress-temporary-known-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumUnknownStrongCellScore: 0.6D,
                MaximumCompositeScore: 0.79D,
                FocusWindowName: "temporary-cell"),
            [
                new SyntheticStressWindow("temporary-cell", 3, 7)
            ]);
    }

    private static SyntheticStressTrace CreateMetricUnavailableWithChurnStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.MetricUnavailableWithChurnStress;
        var builder = new SyntheticStressTraceBuilder(kind);
        string[] servingCells =
        [
            "synthetic-stress-unavailable-known-a",
            "synthetic-stress-unavailable-known-b",
            "synthetic-stress-unavailable-known-a",
            "synthetic-stress-unavailable-known-b",
            "synthetic-stress-unavailable-known-a"
        ];

        for (int index = 0; index < 12; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                index < servingCells.Length
                    ? servingCells[index]
                    : "synthetic-stress-unavailable-known-a",
                StableNeighborGraph,
                servingRsrpDbm: null,
                neighborRsrpDbm: null);
        }

        return CreateTrace(
            kind,
            "Metric unavailable with churn stress",
            "Serving-cell churn with unavailable signal metrics to verify missing metric data does not create high confidence.",
            builder.Build(),
            CreateBaseline("stress-metric-unavailable-with-churn", [.. servingCells, .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumUnknownStrongCellScore: 0.2D,
                MaximumCompositeScore: 0.79D,
                FocusWindowName: "metric-unavailable-churn"),
            [
                new SyntheticStressWindow("metric-unavailable-churn", 0, 12)
            ]);
    }

    private static SyntheticStressTrace CreateNearThresholdBorderOscillationStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.NearThresholdBorderOscillationStress;
        var builder = new SyntheticStressTraceBuilder(kind);
        string[] servingCells =
        [
            "synthetic-stress-border-a",
            "synthetic-stress-border-b",
            "synthetic-stress-border-a",
            "synthetic-stress-border-b"
        ];

        for (int index = 0; index < 4; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                servingCells[index],
                StableNeighborGraph,
                servingRsrpDbm: -83D + (index % 2),
                neighborRsrpDbm: -88D);
        }

        for (int index = 4; index < 10; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-border-a",
                StableNeighborGraph,
                servingRsrpDbm: -83D,
                neighborRsrpDbm: -88D);
        }

        return CreateTrace(
            kind,
            "Near-threshold border oscillation stress",
            "Limited border-like oscillation near handover thresholds with stable neighbor graph and bounded evidence.",
            builder.Build(),
            CreateBaseline("stress-near-threshold-border-oscillation", [.. servingCells, .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumStationaryHandoverScore: 0.79D,
                MaximumNeighborGraphScore: 0.2D,
                MaximumCompositeScore: 0.79D,
                FocusWindowName: "near-threshold-border"),
            [
                new SyntheticStressWindow("near-threshold-border", 0, 6)
            ]);
    }

    private static SyntheticStressTrace CreateLongMixedUrbanDayStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.LongMixedUrbanDayStress;
        var builder = new SyntheticStressTraceBuilder(kind);
        string[] mobilityCells =
        [
            "synthetic-stress-long-mixed-mobile-a",
            "synthetic-stress-long-mixed-mobile-b",
            "synthetic-stress-long-mixed-mobile-c",
            "synthetic-stress-long-mixed-mobile-d"
        ];

        for (int index = 0; index < 8; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-long-mixed-home-a",
                StableNeighborGraph,
                servingRsrpDbm: -83D + (index % 2),
                neighborRsrpDbm: -89D);
        }

        for (int index = 8; index < 16; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                index % 2 == 0 ? DeviceMotionState.Moving : DeviceMotionState.InVehicle,
                mobilityCells[index % mobilityCells.Length],
                index % 2 == 0 ? StableNeighborGraph : SmallDriftNeighborGraph,
                servingRsrpDbm: -81D + (index % 3),
                neighborRsrpDbm: -88D);
        }

        string[] burstServing =
        [
            "synthetic-stress-long-mixed-burst-known-a",
            "synthetic-stress-long-mixed-unknown-strong",
            "synthetic-stress-long-mixed-burst-known-b",
            "synthetic-stress-long-mixed-unknown-strong",
            "synthetic-stress-long-mixed-burst-known-c",
            "synthetic-stress-long-mixed-unknown-strong",
            "synthetic-stress-long-mixed-burst-known-a",
            "synthetic-stress-long-mixed-unknown-strong"
        ];

        for (int index = 0; index < burstServing.Length; index++)
        {
            RadioAccessTechnology rat = index < 2 ? RadioAccessTechnology.Lte : RadioAccessTechnology.Gsm;
            string servingCell = burstServing[index];
            builder.AddServingCellWithNeighbors(
                Offset(16 + index),
                DeviceMotionState.Stationary,
                servingCell,
                index % 2 == 0 ? SevereNeighborGraph : AlternateSevereNeighborGraph,
                servingRsrpDbm: servingCell == "synthetic-stress-long-mixed-unknown-strong" ? -62D : -78D,
                neighborRsrpDbm: -87D,
                radioAccessTechnology: rat);
        }

        for (int index = 24; index < 36; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-long-mixed-home-a",
                StableNeighborGraph,
                servingRsrpDbm: -84D + (index % 2),
                neighborRsrpDbm: -90D);
        }

        return CreateTrace(
            kind,
            "Long mixed urban day stress",
            "Long bounded day-like trace with stable, mobile, suspicious-compatible and recovery windows.",
            builder.Build(),
            CreateBaseline(
                "stress-long-mixed-urban-day",
                ["synthetic-stress-long-mixed-home-a", .. mobilityCells, .. StableNeighborGraph, .. SmallDriftNeighborGraph]),
            new SyntheticStressExpectation(
                MinimumCompositeScore: 0.8D,
                MaximumCompositeScore: 1D,
                FocusWindowName: "focused-burst"),
            [
                new SyntheticStressWindow("morning-stable", 0, 8),
                new SyntheticStressWindow("midday-mobility", 8, 8),
                new SyntheticStressWindow("focused-burst", 16, 8),
                new SyntheticStressWindow("evening-recovery", 24, 8)
            ]);
    }

    private static SyntheticStressTrace CreateSparseEvidenceAmbiguousStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.SparseEvidenceAmbiguousStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 12; index++)
        {
            if (index % 3 == 0)
            {
                builder.AddUnknownRatObservation(Offset(index), DeviceMotionState.Unknown);
            }
            else if (index % 3 == 1)
            {
                builder.AddNeighborOnly(
                    Offset(index),
                    DeviceMotionState.Unknown,
                    ["synthetic-stress-sparse-neighbor-a"],
                    neighborRsrpDbm: null);
            }
            else
            {
                builder.AddServingCellWithNeighbors(
                    Offset(index),
                    DeviceMotionState.Moving,
                    "synthetic-stress-sparse-known-a",
                    [],
                    servingRsrpDbm: null,
                    neighborRsrpDbm: null,
                    radioAccessTechnology: RadioAccessTechnology.Unknown);
            }
        }

        return CreateTrace(
            kind,
            "Sparse evidence ambiguous stress",
            "Sparse unavailable metrics, unknown RAT values and ambiguous motion that should not become high risk.",
            builder.Build(),
            CreateBaseline("stress-sparse-evidence-ambiguous", ["synthetic-stress-sparse-known-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MaximumStationaryHandoverScore: 0.2D,
                MaximumRatDowngradeScore: 0.2D,
                MaximumUnknownStrongCellScore: 0.2D,
                MaximumNeighborGraphScore: 0.2D,
                MaximumCompositeScore: 0.35D,
                FocusWindowName: "sparse-ambiguous"),
            [
                new SyntheticStressWindow("sparse-ambiguous", 0, 12)
            ]);
    }

    private static SyntheticStressTrace CreateAlternatingBenignAndSuspiciousWindowsStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.AlternatingBenignAndSuspiciousWindowsStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        AddStableRun(builder, 0, 6, "synthetic-stress-alternating-known-a");
        AddSuspiciousRun(
            builder,
            6,
            "synthetic-stress-alternating-unknown-a",
            "synthetic-stress-alternating-burst-a");
        AddStableRun(builder, 14, 6, "synthetic-stress-alternating-known-a");
        AddSuspiciousRun(
            builder,
            20,
            "synthetic-stress-alternating-unknown-b",
            "synthetic-stress-alternating-burst-b");
        AddStableRun(builder, 28, 4, "synthetic-stress-alternating-known-a");

        return CreateTrace(
            kind,
            "Alternating benign and suspicious windows stress",
            "Alternating stable and suspicious-compatible windows to verify high risk stays localized.",
            builder.Build(),
            CreateBaseline("stress-alternating-benign-suspicious", ["synthetic-stress-alternating-known-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MinimumCompositeScore: 0.8D,
                MaximumCompositeScore: 1D,
                FocusWindowName: "suspicious-a"),
            [
                new SyntheticStressWindow("benign-a", 0, 6),
                new SyntheticStressWindow("suspicious-a", 6, 8),
                new SyntheticStressWindow("benign-b", 14, 6),
                new SyntheticStressWindow("suspicious-b", 20, 8),
                new SyntheticStressWindow("recovery", 28, 4)
            ]);
    }

    private static SyntheticStressTrace CreateBaselineWarmupThenUnknownCellStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.BaselineWarmupThenUnknownCellStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 3; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-warmup-known-a",
                StableNeighborGraph,
                servingRsrpDbm: -84D);
        }

        for (int index = 3; index < 9; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-warmup-known-a",
                StableNeighborGraph,
                servingRsrpDbm: -83D + (index % 2),
                neighborRsrpDbm: -88D);
        }

        for (int index = 9; index < 17; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-warmup-unknown-strong",
                StableNeighborGraph,
                servingRsrpDbm: -62D,
                neighborRsrpDbm: -88D);
        }

        return CreateTrace(
            kind,
            "Baseline warmup then unknown cell stress",
            "Early warmup remains suppressed before repeated strong unknown serving-cell observations appear.",
            builder.Build(),
            CreateBaseline("stress-baseline-warmup-then-unknown-cell", ["synthetic-stress-warmup-known-a", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MinimumUnknownStrongCellScore: 0.8D,
                MinimumCompositeScore: 0.8D,
                ExpectsEarlyColdStartSuppression: true,
                FocusWindowName: "unknown-after-warmup"),
            [
                new SyntheticStressWindow("early-warmup", 0, 3),
                new SyntheticStressWindow("trusted-warm", 3, 6),
                new SyntheticStressWindow("unknown-after-warmup", 9, 8)
            ]);
    }

    private static SyntheticStressTrace CreateNeighborGraphPartialRecoveryStress()
    {
        const SyntheticStressTraceKind kind = SyntheticStressTraceKind.NeighborGraphPartialRecoveryStress;
        var builder = new SyntheticStressTraceBuilder(kind);

        for (int index = 0; index < 6; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-graph-recovery-serving",
                StableNeighborGraph,
                servingRsrpDbm: -83D,
                neighborRsrpDbm: -88D);
        }

        for (int index = 6; index < 14; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-graph-recovery-serving",
                SevereNeighborGraph,
                servingRsrpDbm: -82D,
                neighborRsrpDbm: -87D);
        }

        for (int index = 14; index < 22; index++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(index),
                DeviceMotionState.Stationary,
                "synthetic-stress-graph-recovery-serving",
                index == 14 ? SmallDriftNeighborGraph : StableNeighborGraph,
                servingRsrpDbm: -83D,
                neighborRsrpDbm: -88D);
        }

        return CreateTrace(
            kind,
            "Neighbor graph partial recovery stress",
            "Severe stationary neighbor graph drift followed by partial similarity recovery.",
            builder.Build(),
            CreateBaseline("stress-neighbor-graph-partial-recovery", ["synthetic-stress-graph-recovery-serving", .. StableNeighborGraph]),
            new SyntheticStressExpectation(
                MinimumNeighborGraphScore: 0.8D,
                MaximumCompositeScore: 1D,
                FocusWindowName: "severe-drift"),
            [
                new SyntheticStressWindow("stable-before-drift", 0, 6),
                new SyntheticStressWindow("severe-drift", 6, 8),
                new SyntheticStressWindow("partial-recovery", 14, 8)
            ]);
    }

    private static void AddStableRun(
        SyntheticStressTraceBuilder builder,
        int startIndex,
        int count,
        string servingCell)
    {
        for (int offset = 0; offset < count; offset++)
        {
            builder.AddServingCellWithNeighbors(
                Offset(startIndex + offset),
                DeviceMotionState.Stationary,
                servingCell,
                StableNeighborGraph,
                servingRsrpDbm: -83D + (offset % 2),
                neighborRsrpDbm: -88D);
        }
    }

    private static void AddSuspiciousRun(
        SyntheticStressTraceBuilder builder,
        int startIndex,
        string unknownServingCell,
        string knownServingPrefix)
    {
        string[] servingCells =
        [
            $"{knownServingPrefix}-known-a",
            unknownServingCell,
            $"{knownServingPrefix}-known-b",
            unknownServingCell,
            $"{knownServingPrefix}-known-c",
            unknownServingCell,
            $"{knownServingPrefix}-known-a",
            unknownServingCell
        ];

        for (int index = 0; index < servingCells.Length; index++)
        {
            RadioAccessTechnology rat = index < 2 ? RadioAccessTechnology.Lte : RadioAccessTechnology.Gsm;
            string servingCell = servingCells[index];
            builder.AddServingCellWithNeighbors(
                Offset(startIndex + index),
                DeviceMotionState.Stationary,
                servingCell,
                index % 2 == 0 ? SevereNeighborGraph : AlternateSevereNeighborGraph,
                servingRsrpDbm: servingCell == unknownServingCell ? -62D : -78D,
                neighborRsrpDbm: -87D,
                radioAccessTechnology: rat);
        }
    }

    private static SyntheticStressTrace CreateTrace(
        SyntheticStressTraceKind kind,
        string name,
        string description,
        ReplayTrace trace,
        BaselineProfile baseline,
        SyntheticStressExpectation expectation,
        IReadOnlyList<SyntheticStressWindow>? windows = null)
    {
        return new SyntheticStressTrace(
            kind,
            name,
            description,
            trace,
            baseline,
            expectation,
            windows ?? []);
    }

    private static TimeSpan Offset(int index)
    {
        return TimeSpan.FromSeconds(30D * index);
    }

    private static BaselineProfile CreateBaseline(string profileId, IReadOnlyList<string> syntheticCellIds)
    {
        TrustedCellSignature[] trustedCells = syntheticCellIds
            .Distinct(StringComparer.Ordinal)
            .Select(static syntheticCellId => new TrustedCellSignature(
                string.Create(CultureInfo.InvariantCulture, $"trusted:{syntheticCellId}"),
                RadioAccessTechnology.Lte,
                NetworkCellPseudonym: syntheticCellId,
                ChannelNumber: 6400))
            .ToArray();
        DateTimeOffset createdAt = SyntheticStressTraceBuilder.BaseTimestamp.AddDays(-14D);

        return new BaselineProfile(profileId, createdAt, SyntheticStressTraceBuilder.BaseTimestamp, trustedCells);
    }
}
