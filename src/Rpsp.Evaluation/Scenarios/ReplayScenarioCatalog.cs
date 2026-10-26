using System.Globalization;
using Rpsp.Core.Baselines;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;

namespace Rpsp.Evaluation.Scenarios;

public static class ReplayScenarioCatalog
{
    private static readonly string[] TrustedNeighborGraphCells =
    [
        "synthetic-neighbor-a",
        "synthetic-neighbor-b",
        "synthetic-neighbor-c"
    ];

    private static readonly string[] SmallDriftNeighborGraphCells =
    [
        "synthetic-neighbor-a",
        "synthetic-neighbor-b",
        "synthetic-neighbor-x"
    ];

    private static readonly string[] SevereDriftNeighborGraphCells =
    [
        "synthetic-neighbor-x",
        "synthetic-neighbor-y",
        "synthetic-neighbor-z"
    ];

    private static readonly string[] AlternateSevereDriftNeighborGraphCells =
    [
        "synthetic-neighbor-p",
        "synthetic-neighbor-q",
        "synthetic-neighbor-r"
    ];

    private static readonly string[] SecondAlternateSevereDriftNeighborGraphCells =
    [
        "synthetic-neighbor-l",
        "synthetic-neighbor-m",
        "synthetic-neighbor-n"
    ];

    private static readonly string[] MissingNeighborServingSequence =
    [
        "synthetic-serving-a",
        "synthetic-serving-a",
        "synthetic-serving-a",
        "synthetic-serving-a",
        "synthetic-serving-a"
    ];

    private static readonly (string ServingCellId, string[] NeighborCellIds)[] StableKnownNeighborGraphFrames =
    [
        ("synthetic-serving-a", TrustedNeighborGraphCells),
        ("synthetic-serving-a", TrustedNeighborGraphCells),
        ("synthetic-serving-a", TrustedNeighborGraphCells),
        ("synthetic-serving-a", TrustedNeighborGraphCells),
        ("synthetic-serving-a", TrustedNeighborGraphCells)
    ];

    private static readonly (string ServingCellId, string[] NeighborCellIds)[] SmallNeighborGraphDriftFrames =
    [
        ("synthetic-serving-a", TrustedNeighborGraphCells),
        ("synthetic-serving-a", SmallDriftNeighborGraphCells),
        ("synthetic-serving-a", TrustedNeighborGraphCells),
        ("synthetic-serving-a", SmallDriftNeighborGraphCells),
        ("synthetic-serving-a", TrustedNeighborGraphCells)
    ];

    private static readonly (string ServingCellId, string[] NeighborCellIds)[] StationaryLargeNeighborGraphDriftFrames =
    [
        ("synthetic-serving-a", TrustedNeighborGraphCells),
        ("synthetic-serving-a", SevereDriftNeighborGraphCells),
        ("synthetic-serving-a", SevereDriftNeighborGraphCells),
        ("synthetic-serving-a", TrustedNeighborGraphCells),
        ("synthetic-serving-a", TrustedNeighborGraphCells)
    ];

    private static readonly (string ServingCellId, string[] NeighborCellIds)[] RepeatedSevereNeighborGraphDriftFrames =
    [
        ("synthetic-serving-a", SevereDriftNeighborGraphCells),
        ("synthetic-serving-a", SevereDriftNeighborGraphCells),
        ("synthetic-serving-a", SevereDriftNeighborGraphCells),
        ("synthetic-serving-a", SevereDriftNeighborGraphCells),
        ("synthetic-serving-a", SevereDriftNeighborGraphCells)
    ];

    private static readonly (string ServingCellId, string[] NeighborCellIds)[] MovingNeighborGraphDriftFrames =
    [
        ("synthetic-serving-a", TrustedNeighborGraphCells),
        ("synthetic-serving-b", SevereDriftNeighborGraphCells),
        ("synthetic-serving-c", AlternateSevereDriftNeighborGraphCells),
        ("synthetic-serving-d", SecondAlternateSevereDriftNeighborGraphCells),
        ("synthetic-serving-e", TrustedNeighborGraphCells)
    ];

    private static readonly (string ServingCellId, string[] NeighborCellIds)[] ServingChangeWithGraphDriftFrames =
    [
        ("synthetic-serving-a", TrustedNeighborGraphCells),
        ("synthetic-serving-b", SevereDriftNeighborGraphCells),
        ("synthetic-serving-b", SevereDriftNeighborGraphCells),
        ("synthetic-serving-c", SevereDriftNeighborGraphCells),
        ("synthetic-serving-c", TrustedNeighborGraphCells)
    ];

    public static IReadOnlyList<ReplayScenario> All { get; } =
    [
        CreateStationaryStableServingCell(),
        CreateStationarySuspiciousChurn(),
        CreateStationaryHighChurn(),
        CreateMovingUrbanBenignHandover(),
        CreateInVehicleFastBenignHandover(),
        CreateIndoorFadingWithoutCellChurn(),
        CreateBorderCellOscillationLowRisk(),
        CreateColdStartInsufficientObservations(),
        CreateMissingServingCells(),
        CreateOutOfOrderInputNormalizedByReplay(),
        CreateStableLteNoDowngrade(),
        CreateStableNrNoDowngrade(),
        CreateSuspiciousLteToUmtsDowngrade(),
        CreateHighRiskLteToGsmDowngrade(),
        CreateNrToLteBenignFallback(),
        CreateMovingCoverageLossBenignDowngrade(),
        CreateIndoorCoverageDegradationBenign(),
        CreateNoServiceThenLegacyRatSuspicious(),
        CreateColdStartInsufficientRatHistory(),
        CreateUnknownRatValuesAreIgnoredOrSuppressed(),
        CreateKnownTrustedCellsStable(),
        CreateUnknownWeakNeighborCell(),
        CreateUnknownStrongServingCellStationary(),
        CreateRepeatedUnknownVeryStrongServingCellStationary(),
        CreateUnknownStrongNeighborOnly(),
        CreateMovingUnknownStrongCellSuppressed(),
        CreateEmptyBaselineSuppressesUnknownCell(),
        CreateUnavailableSignalDoesNotTriggerHighRisk(),
        CreateStableKnownNeighborGraph(),
        CreateSmallNeighborGraphDrift(),
        CreateStationaryLargeNeighborGraphDrift(),
        CreateRepeatedSevereNeighborGraphDrift(),
        CreateMovingNeighborGraphDriftSuppressed(),
        CreateMissingNeighborCellsSuppressed(),
        CreateInsufficientNeighborBaselineSuppressed(),
        CreateServingChangeWithGraphDrift()
    ];

    public static ReplayScenario Get(ReplayScenarioKind kind)
    {
        return All.Single(scenario => scenario.Kind == kind);
    }

    private static ReplayScenario CreateStationaryStableServingCell()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.StationaryStableServingCell)
            .AddServingCell(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-cell-a", -82D, -9D, 18D)
            .AddServingCell(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary, "synthetic-cell-a", -83D, -10D, 17D)
            .AddServingCell(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary, "synthetic-cell-a", -81D, -9D, 19D)
            .AddServingCell(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary, "synthetic-cell-a", -84D, -10D, 16D)
            .AddServingCell(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary, "synthetic-cell-a", -82D, -9D, 18D)
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.StationaryStableServingCell,
            "Stationary stable serving cell",
            "Stationary device with one stable serving cell and small signal variation.",
            trace,
            ExpectedStationaryHandoverSuppression: true,
            MaximumStationaryHandoverScore: 0.1D,
            ExpectedStationaryHandoverSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateStationarySuspiciousChurn()
    {
        ReplayTrace trace = BuildServingSequence(
            ReplayScenarioKind.StationarySuspiciousChurn,
            DeviceMotionState.Stationary,
            ["synthetic-cell-a", "synthetic-cell-b", "synthetic-cell-a", "synthetic-cell-c", "synthetic-cell-b"],
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.StationarySuspiciousChurn,
            "Stationary suspicious churn",
            "Stationary device with repeated serving-cell changes across three cells.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            MinimumStationaryHandoverScore: 0.6D,
            ExpectedStationaryHandoverSeverity: RiskSeverity.Medium);
    }

    private static ReplayScenario CreateStationaryHighChurn()
    {
        ReplayTrace trace = BuildServingSequence(
            ReplayScenarioKind.StationaryHighChurn,
            DeviceMotionState.Stationary,
            [
                "synthetic-cell-a",
                "synthetic-cell-b",
                "synthetic-cell-c",
                "synthetic-cell-d",
                "synthetic-cell-a",
                "synthetic-cell-b",
                "synthetic-cell-c"
            ],
            TimeSpan.FromSeconds(20D));

        return new ReplayScenario(
            ReplayScenarioKind.StationaryHighChurn,
            "Stationary high churn",
            "Stationary device with high-rate serving-cell churn across four cells.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            MinimumStationaryHandoverScore: 0.8D,
            ExpectedStationaryHandoverSeverity: RiskSeverity.High);
    }

    private static ReplayScenario CreateMovingUrbanBenignHandover()
    {
        ReplayTrace trace = BuildServingSequence(
            ReplayScenarioKind.MovingUrbanBenignHandover,
            DeviceMotionState.Moving,
            [
                "synthetic-cell-a",
                "synthetic-cell-b",
                "synthetic-cell-c",
                "synthetic-cell-d",
                "synthetic-cell-e",
                "synthetic-cell-f"
            ],
            TimeSpan.FromSeconds(25D));

        return new ReplayScenario(
            ReplayScenarioKind.MovingUrbanBenignHandover,
            "Moving urban benign handover",
            "Moving device with sequential serving-cell changes treated as benign mobility.",
            trace,
            ExpectedStationaryHandoverSuppression: true,
            MaximumStationaryHandoverScore: 0.1D,
            ExpectedStationaryHandoverSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateInVehicleFastBenignHandover()
    {
        ReplayTrace trace = BuildServingSequence(
            ReplayScenarioKind.InVehicleFastBenignHandover,
            DeviceMotionState.InVehicle,
            [
                "synthetic-cell-a",
                "synthetic-cell-b",
                "synthetic-cell-c",
                "synthetic-cell-d",
                "synthetic-cell-e",
                "synthetic-cell-f",
                "synthetic-cell-g"
            ],
            TimeSpan.FromSeconds(15D));

        return new ReplayScenario(
            ReplayScenarioKind.InVehicleFastBenignHandover,
            "In-vehicle fast benign handover",
            "In-vehicle movement with frequent serving-cell changes suppressed by motion context.",
            trace,
            ExpectedStationaryHandoverSuppression: true,
            MaximumStationaryHandoverScore: 0.1D,
            ExpectedStationaryHandoverSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateIndoorFadingWithoutCellChurn()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.IndoorFadingWithoutCellChurn)
            .AddServingCell(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-cell-a", -80D, -8D, 19D)
            .AddServingCell(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary, "synthetic-cell-a", -95D, -13D, 8D)
            .AddServingCell(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary, "synthetic-cell-a", -108D, -17D, 2D)
            .AddServingCell(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary, "synthetic-cell-a", -92D, -12D, 9D)
            .AddServingCell(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary, "synthetic-cell-a", -83D, -9D, 17D)
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.IndoorFadingWithoutCellChurn,
            "Indoor fading without cell churn",
            "Stationary device with degraded and recovered signal metrics but no serving-cell change.",
            trace,
            ExpectedStationaryHandoverSuppression: true,
            MaximumStationaryHandoverScore: 0.1D,
            ExpectedStationaryHandoverSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateBorderCellOscillationLowRisk()
    {
        ReplayTrace trace = BuildServingSequence(
            ReplayScenarioKind.BorderCellOscillationLowRisk,
            DeviceMotionState.Walking,
            ["synthetic-cell-a", "synthetic-cell-b", "synthetic-cell-a", "synthetic-cell-a", "synthetic-cell-a"],
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.BorderCellOscillationLowRisk,
            "Border cell oscillation low risk",
            "Low-mobility edge case with limited oscillation between two serving cells.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            MaximumStationaryHandoverScore: 0.4D,
            ExpectedStationaryHandoverSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateColdStartInsufficientObservations()
    {
        ReplayTrace trace = BuildServingSequence(
            ReplayScenarioKind.ColdStartInsufficientObservations,
            DeviceMotionState.Stationary,
            ["synthetic-cell-a", "synthetic-cell-b", "synthetic-cell-a"],
            TimeSpan.FromSeconds(20D));

        return new ReplayScenario(
            ReplayScenarioKind.ColdStartInsufficientObservations,
            "Cold start insufficient observations",
            "Stationary churn-like sequence with fewer observations than the detector minimum.",
            trace,
            ExpectedStationaryHandoverSuppression: true,
            MaximumStationaryHandoverScore: 0.1D,
            ExpectedStationaryHandoverSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateMissingServingCells()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.MissingServingCells)
            .AddObservationWithoutServingCell(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-neighbor-a")
            .AddObservationWithoutServingCell(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary, "synthetic-neighbor-b")
            .AddObservationWithoutServingCell(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary, "synthetic-neighbor-c")
            .AddObservationWithoutServingCell(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary, "synthetic-neighbor-b")
            .AddObservationWithoutServingCell(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary, "synthetic-neighbor-a")
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.MissingServingCells,
            "Missing serving cells",
            "Observations with neighbor-only radio context and no serving-cell snapshots.",
            trace,
            ExpectedStationaryHandoverSuppression: true,
            MaximumStationaryHandoverScore: 0.1D,
            ExpectedStationaryHandoverSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateOutOfOrderInputNormalizedByReplay()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.OutOfOrderInputNormalizedByReplay)
            .AddServingCell(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary, "synthetic-cell-a", -82D, -9D, 18D)
            .AddServingCell(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-cell-a", -81D, -9D, 18D)
            .AddServingCell(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary, "synthetic-cell-b", -83D, -10D, 17D)
            .AddServingCell(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary, "synthetic-cell-b", -82D, -9D, 18D)
            .AddServingCell(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary, "synthetic-cell-c", -84D, -10D, 16D)
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.OutOfOrderInputNormalizedByReplay,
            "Out-of-order input normalized by replay",
            "Frames are added out of order and normalized by replay trace ordering.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            MinimumStationaryHandoverScore: 0.6D,
            ExpectedStationaryHandoverSeverity: RiskSeverity.Medium);
    }

    private static ReplayScenario CreateStableLteNoDowngrade()
    {
        ReplayTrace trace = BuildRatSequence(
            ReplayScenarioKind.StableLteNoDowngrade,
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte
            ],
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.StableLteNoDowngrade,
            "Stable LTE no downgrade",
            "Stationary device with stable LTE radio access.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            HasRatDowngradeExpectation: true,
            ExpectedRatDowngradeSuppression: true,
            MaximumRatDowngradeScore: 0.1D,
            ExpectedRatDowngradeSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateStableNrNoDowngrade()
    {
        ReplayTrace trace = BuildRatSequence(
            ReplayScenarioKind.StableNrNoDowngrade,
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Nr
            ],
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.StableNrNoDowngrade,
            "Stable NR no downgrade",
            "Stationary device with stable NR radio access.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            HasRatDowngradeExpectation: true,
            ExpectedRatDowngradeSuppression: true,
            MaximumRatDowngradeScore: 0.1D,
            ExpectedRatDowngradeSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateSuspiciousLteToUmtsDowngrade()
    {
        ReplayTrace trace = BuildRatSequence(
            ReplayScenarioKind.SuspiciousLteToUmtsDowngrade,
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Umts,
                RadioAccessTechnology.Umts
            ],
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.SuspiciousLteToUmtsDowngrade,
            "Suspicious LTE to UMTS downgrade",
            "Stationary modern RAT history followed by a short UMTS dwell.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            HasRatDowngradeExpectation: true,
            ExpectedRatDowngradeSuppression: false,
            MinimumRatDowngradeScore: 0.6D,
            ExpectedRatDowngradeSeverity: RiskSeverity.Medium);
    }

    private static ReplayScenario CreateHighRiskLteToGsmDowngrade()
    {
        ReplayTrace trace = BuildRatSequence(
            ReplayScenarioKind.HighRiskLteToGsmDowngrade,
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Gsm,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Gsm,
                RadioAccessTechnology.Gsm
            ],
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.HighRiskLteToGsmDowngrade,
            "High risk LTE to GSM downgrade",
            "Stationary modern RAT history followed by repeated GSM legacy downgrade transitions.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            HasRatDowngradeExpectation: true,
            ExpectedRatDowngradeSuppression: false,
            MinimumRatDowngradeScore: 0.8D,
            ExpectedRatDowngradeSeverity: RiskSeverity.High);
    }

    private static ReplayScenario CreateNrToLteBenignFallback()
    {
        ReplayTrace trace = BuildRatSequence(
            ReplayScenarioKind.NrToLteBenignFallback,
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Lte
            ],
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.NrToLteBenignFallback,
            "NR to LTE benign fallback",
            "NR changes to LTE without entering legacy radio access.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            HasRatDowngradeExpectation: true,
            ExpectedRatDowngradeSuppression: true,
            MaximumRatDowngradeScore: 0.1D,
            ExpectedRatDowngradeSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateMovingCoverageLossBenignDowngrade()
    {
        ReplayTrace trace = BuildRatSequence(
            ReplayScenarioKind.MovingCoverageLossBenignDowngrade,
            DeviceMotionState.Moving,
            [
                RadioAccessTechnology.Nr,
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Umts,
                RadioAccessTechnology.Gsm,
                RadioAccessTechnology.Gsm
            ],
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.MovingCoverageLossBenignDowngrade,
            "Moving coverage loss benign downgrade",
            "Moving device with modern-to-legacy RAT changes suppressed by motion context.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            HasRatDowngradeExpectation: true,
            ExpectedRatDowngradeSuppression: true,
            MaximumRatDowngradeScore: 0.1D,
            ExpectedRatDowngradeSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateIndoorCoverageDegradationBenign()
    {
        var builder = new RadioObservationScenarioBuilder(ReplayScenarioKind.IndoorCoverageDegradationBenign)
            .AddServingCell(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-cell-a", -80D, -8D, 19D, RadioAccessTechnology.Lte)
            .AddServingCell(TimeSpan.FromSeconds(20D), DeviceMotionState.Stationary, "synthetic-cell-a", -101D, -15D, 4D, RadioAccessTechnology.Lte)
            .AddServingCell(TimeSpan.FromSeconds(40D), DeviceMotionState.Stationary, "synthetic-cell-b", -106D, -17D, 2D, RadioAccessTechnology.Umts)
            .AddServingCell(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary, "synthetic-cell-a", -94D, -12D, 8D, RadioAccessTechnology.Lte)
            .AddServingCell(TimeSpan.FromSeconds(80D), DeviceMotionState.Stationary, "synthetic-cell-a", -84D, -9D, 16D, RadioAccessTechnology.Lte);

        return new ReplayScenario(
            ReplayScenarioKind.IndoorCoverageDegradationBenign,
            "Indoor coverage degradation benign",
            "Stationary signal degradation with one short legacy RAT interval and recovery.",
            builder.Build(),
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            HasRatDowngradeExpectation: true,
            ExpectedRatDowngradeSuppression: false,
            MinimumRatDowngradeScore: 0.6D,
            MaximumRatDowngradeScore: 0.79D,
            ExpectedRatDowngradeSeverity: RiskSeverity.Medium);
    }

    private static ReplayScenario CreateNoServiceThenLegacyRatSuspicious()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.NoServiceThenLegacyRatSuspicious)
            .AddServingCell(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-cell-a", radioAccessTechnology: RadioAccessTechnology.Lte)
            .AddServingCell(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary, "synthetic-cell-a", radioAccessTechnology: RadioAccessTechnology.Lte)
            .AddServiceUnavailable(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary)
            .AddServingCell(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary, "synthetic-cell-b", radioAccessTechnology: RadioAccessTechnology.Umts)
            .AddServingCell(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary, "synthetic-cell-b", radioAccessTechnology: RadioAccessTechnology.Umts)
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.NoServiceThenLegacyRatSuspicious,
            "NoService then legacy RAT suspicious",
            "Stable modern RAT history followed by service unavailable state and legacy RAT.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            HasRatDowngradeExpectation: true,
            ExpectedRatDowngradeSuppression: false,
            MinimumRatDowngradeScore: 0.8D,
            ExpectedRatDowngradeSeverity: RiskSeverity.High);
    }

    private static ReplayScenario CreateColdStartInsufficientRatHistory()
    {
        ReplayTrace trace = BuildRatSequence(
            ReplayScenarioKind.ColdStartInsufficientRatHistory,
            DeviceMotionState.Stationary,
            [
                RadioAccessTechnology.Lte,
                RadioAccessTechnology.Umts,
                RadioAccessTechnology.Lte
            ],
            TimeSpan.FromSeconds(20D));

        return new ReplayScenario(
            ReplayScenarioKind.ColdStartInsufficientRatHistory,
            "Cold start insufficient RAT history",
            "Legacy RAT appears before the detector has enough observations and modern RAT history.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            HasRatDowngradeExpectation: true,
            ExpectedRatDowngradeSuppression: true,
            MaximumRatDowngradeScore: 0.1D,
            ExpectedRatDowngradeSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateUnknownRatValuesAreIgnoredOrSuppressed()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.UnknownRatValuesAreIgnoredOrSuppressed)
            .AddUnknownRatObservation(TimeSpan.Zero, DeviceMotionState.Stationary)
            .AddUnknownRatObservation(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary)
            .AddUnknownRatObservation(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary)
            .AddUnknownRatObservation(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary)
            .AddUnknownRatObservation(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary)
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.UnknownRatValuesAreIgnoredOrSuppressed,
            "Unknown RAT values are ignored or suppressed",
            "Unavailable RAT values do not create a high-risk downgrade result.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            HasRatDowngradeExpectation: true,
            ExpectedRatDowngradeSuppression: true,
            MaximumRatDowngradeScore: 0.1D,
            ExpectedRatDowngradeSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateKnownTrustedCellsStable()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.KnownTrustedCellsStable)
            .AddServingCellWithNeighbor(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-known-b", -82D, -86D)
            .AddServingCellWithNeighbor(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-known-b", -81D, -87D)
            .AddServingCellWithNeighbor(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-known-b", -83D, -85D)
            .AddServingCellWithNeighbor(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-known-b", -82D, -86D)
            .AddServingCellWithNeighbor(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-known-b", -82D, -86D)
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.KnownTrustedCellsStable,
            "Known trusted cells stable",
            "Stationary observations where all observed cells are present in the local trusted baseline.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("known-trusted-cells-stable", "synthetic-known-a", "synthetic-known-b"),
            HasUnknownStrongCellExpectation: true,
            ExpectedUnknownStrongCellSuppression: true,
            MaximumUnknownStrongCellScore: 0.1D,
            ExpectedUnknownStrongCellSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateUnknownWeakNeighborCell()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.UnknownWeakNeighborCell)
            .AddServingCellWithNeighbor(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-unknown-weak", -82D, -101D)
            .AddServingCellWithNeighbor(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-unknown-weak", -82D, -102D)
            .AddServingCellWithNeighbor(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-unknown-weak", -83D, -100D)
            .AddServingCellWithNeighbor(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-unknown-weak", -82D, -103D)
            .AddServingCellWithNeighbor(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-unknown-weak", -81D, -101D)
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.UnknownWeakNeighborCell,
            "Unknown weak neighbor cell",
            "Stationary trusted serving cell with an unknown weak neighbor that should not become high risk.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("unknown-weak-neighbor-cell", "synthetic-known-a", "synthetic-known-b"),
            HasUnknownStrongCellExpectation: true,
            ExpectedUnknownStrongCellSuppression: true,
            MaximumUnknownStrongCellScore: 0.1D,
            ExpectedUnknownStrongCellSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateUnknownStrongServingCellStationary()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.UnknownStrongServingCellStationary)
            .AddServingCell(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-known-a", -83D)
            .AddServingCell(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary, "synthetic-unknown-strong", -74D)
            .AddServingCell(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary, "synthetic-unknown-strong", -73D)
            .AddServingCell(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary, "synthetic-unknown-strong", -72D)
            .AddServingCell(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary, "synthetic-known-a", -82D)
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.UnknownStrongServingCellStationary,
            "Unknown strong serving cell stationary",
            "Stationary local baseline sees an unknown serving cell with strong signal in repeated observations.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("unknown-strong-serving-cell-stationary", "synthetic-known-a", "synthetic-known-b"),
            HasUnknownStrongCellExpectation: true,
            ExpectedUnknownStrongCellSuppression: false,
            MinimumUnknownStrongCellScore: 0.6D,
            ExpectedUnknownStrongCellSeverity: RiskSeverity.Medium);
    }

    private static ReplayScenario CreateRepeatedUnknownVeryStrongServingCellStationary()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.RepeatedUnknownVeryStrongServingCellStationary)
            .AddServingCell(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-known-a", -83D)
            .AddServingCell(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary, "synthetic-unknown-very-strong", -63D)
            .AddServingCell(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary, "synthetic-unknown-very-strong", -62D)
            .AddServingCell(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary, "synthetic-unknown-very-strong", -64D)
            .AddServingCell(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary, "synthetic-unknown-very-strong", -63D)
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.RepeatedUnknownVeryStrongServingCellStationary,
            "Repeated unknown very strong serving cell stationary",
            "Stationary local baseline sees repeated very strong unknown serving-cell observations.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("repeated-unknown-very-strong-serving-cell-stationary", "synthetic-known-a", "synthetic-known-b"),
            HasUnknownStrongCellExpectation: true,
            ExpectedUnknownStrongCellSuppression: false,
            MinimumUnknownStrongCellScore: 0.8D,
            ExpectedUnknownStrongCellSeverity: RiskSeverity.High);
    }

    private static ReplayScenario CreateUnknownStrongNeighborOnly()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.UnknownStrongNeighborOnly)
            .AddServingCellWithNeighbor(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-unknown-neighbor-strong", -84D, -69D)
            .AddServingCellWithNeighbor(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-unknown-neighbor-strong", -83D, -68D)
            .AddServingCellWithNeighbor(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-unknown-neighbor-strong", -82D, -69D)
            .AddServingCellWithNeighbor(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-unknown-neighbor-strong", -83D, -70D)
            .AddServingCellWithNeighbor(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary, "synthetic-known-a", "synthetic-unknown-neighbor-strong", -84D, -69D)
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.UnknownStrongNeighborOnly,
            "Unknown strong neighbor only",
            "Unknown strong cell is repeatedly visible only as a neighbor and does not become serving.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("unknown-strong-neighbor-only", "synthetic-known-a", "synthetic-known-b"),
            HasUnknownStrongCellExpectation: true,
            ExpectedUnknownStrongCellSuppression: false,
            MaximumUnknownStrongCellScore: 0.59D,
            ExpectedUnknownStrongCellSeverity: RiskSeverity.Low);
    }

    private static ReplayScenario CreateMovingUnknownStrongCellSuppressed()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.MovingUnknownStrongCellSuppressed)
            .AddServingCell(TimeSpan.Zero, DeviceMotionState.Moving, "synthetic-known-a", -83D)
            .AddServingCell(TimeSpan.FromSeconds(30D), DeviceMotionState.Moving, "synthetic-unknown-strong", -70D)
            .AddServingCell(TimeSpan.FromSeconds(60D), DeviceMotionState.Moving, "synthetic-unknown-strong", -69D)
            .AddServingCell(TimeSpan.FromSeconds(90D), DeviceMotionState.Moving, "synthetic-unknown-strong", -70D)
            .AddServingCell(TimeSpan.FromSeconds(120D), DeviceMotionState.Moving, "synthetic-known-b", -82D)
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.MovingUnknownStrongCellSuppressed,
            "Moving unknown strong cell suppressed",
            "Moving context suppresses strong unknown-cell observations that may reflect changing coverage.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("moving-unknown-strong-cell-suppressed", "synthetic-known-a", "synthetic-known-b"),
            HasUnknownStrongCellExpectation: true,
            ExpectedUnknownStrongCellSuppression: true,
            MaximumUnknownStrongCellScore: 0.1D,
            ExpectedUnknownStrongCellSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateEmptyBaselineSuppressesUnknownCell()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.EmptyBaselineSuppressesUnknownCell)
            .AddServingCell(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-unknown-strong", -72D)
            .AddServingCell(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary, "synthetic-unknown-strong", -72D)
            .AddServingCell(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary, "synthetic-unknown-strong", -71D)
            .AddServingCell(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary, "synthetic-unknown-strong", -72D)
            .AddServingCell(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary, "synthetic-unknown-strong", -71D)
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.EmptyBaselineSuppressesUnknownCell,
            "Empty baseline suppresses unknown cell",
            "Unknown strong serving observations are suppressed when the trusted local baseline is empty.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("empty-baseline-suppresses-unknown-cell"),
            HasUnknownStrongCellExpectation: true,
            ExpectedUnknownStrongCellSuppression: true,
            MaximumUnknownStrongCellScore: 0.1D,
            ExpectedUnknownStrongCellSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateUnavailableSignalDoesNotTriggerHighRisk()
    {
        ReplayTrace trace = new RadioObservationScenarioBuilder(ReplayScenarioKind.UnavailableSignalDoesNotTriggerHighRisk)
            .AddServingCellWithUnavailableSignal(TimeSpan.Zero, DeviceMotionState.Stationary, "synthetic-unknown-unavailable")
            .AddServingCellWithUnavailableSignal(TimeSpan.FromSeconds(30D), DeviceMotionState.Stationary, "synthetic-unknown-unavailable")
            .AddServingCellWithUnavailableSignal(TimeSpan.FromSeconds(60D), DeviceMotionState.Stationary, "synthetic-unknown-unavailable")
            .AddServingCellWithUnavailableSignal(TimeSpan.FromSeconds(90D), DeviceMotionState.Stationary, "synthetic-unknown-unavailable")
            .AddServingCellWithUnavailableSignal(TimeSpan.FromSeconds(120D), DeviceMotionState.Stationary, "synthetic-unknown-unavailable")
            .Build();

        return new ReplayScenario(
            ReplayScenarioKind.UnavailableSignalDoesNotTriggerHighRisk,
            "Unavailable signal does not trigger high risk",
            "Unknown serving cell identity without available RSRP is not enough for high-risk classification.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("unavailable-signal-does-not-trigger-high-risk", "synthetic-known-a", "synthetic-known-b"),
            HasUnknownStrongCellExpectation: true,
            ExpectedUnknownStrongCellSuppression: true,
            MaximumUnknownStrongCellScore: 0.1D,
            ExpectedUnknownStrongCellSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateStableKnownNeighborGraph()
    {
        ReplayTrace trace = BuildNeighborGraphSequence(
            ReplayScenarioKind.StableKnownNeighborGraph,
            DeviceMotionState.Stationary,
            StableKnownNeighborGraphFrames,
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.StableKnownNeighborGraph,
            "Stable known neighbor graph",
            "Stationary serving cell with a stable neighbor graph matching trusted baseline.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("stable-known-neighbor-graph", TrustedNeighborGraphCells),
            HasNeighborGraphExpectation: true,
            ExpectedNeighborGraphSuppression: true,
            MaximumNeighborGraphScore: 0.2D,
            ExpectedNeighborGraphSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateSmallNeighborGraphDrift()
    {
        ReplayTrace trace = BuildNeighborGraphSequence(
            ReplayScenarioKind.SmallNeighborGraphDrift,
            DeviceMotionState.Stationary,
            SmallNeighborGraphDriftFrames,
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.SmallNeighborGraphDrift,
            "Small neighbor graph drift",
            "Stationary neighbor graph with one neighbor variation and preserved similarity.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("small-neighbor-graph-drift", TrustedNeighborGraphCells),
            HasNeighborGraphExpectation: true,
            ExpectedNeighborGraphSuppression: true,
            MaximumNeighborGraphScore: 0.3D,
            ExpectedNeighborGraphSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateStationaryLargeNeighborGraphDrift()
    {
        ReplayTrace trace = BuildNeighborGraphSequence(
            ReplayScenarioKind.StationaryLargeNeighborGraphDrift,
            DeviceMotionState.Stationary,
            StationaryLargeNeighborGraphDriftFrames,
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.StationaryLargeNeighborGraphDrift,
            "Stationary large neighbor graph drift",
            "Stationary neighbor graph differs sharply from trusted baseline for multiple observations.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("stationary-large-neighbor-graph-drift", TrustedNeighborGraphCells),
            HasNeighborGraphExpectation: true,
            ExpectedNeighborGraphSuppression: false,
            MinimumNeighborGraphScore: 0.6D,
            ExpectedNeighborGraphSeverity: RiskSeverity.High);
    }

    private static ReplayScenario CreateRepeatedSevereNeighborGraphDrift()
    {
        ReplayTrace trace = BuildNeighborGraphSequence(
            ReplayScenarioKind.RepeatedSevereNeighborGraphDrift,
            DeviceMotionState.Stationary,
            RepeatedSevereNeighborGraphDriftFrames,
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.RepeatedSevereNeighborGraphDrift,
            "Repeated severe neighbor graph drift",
            "Stationary neighbor graph remains far from trusted baseline across repeated observations.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("repeated-severe-neighbor-graph-drift", TrustedNeighborGraphCells),
            HasNeighborGraphExpectation: true,
            ExpectedNeighborGraphSuppression: false,
            MinimumNeighborGraphScore: 0.8D,
            ExpectedNeighborGraphSeverity: RiskSeverity.High);
    }

    private static ReplayScenario CreateMovingNeighborGraphDriftSuppressed()
    {
        ReplayTrace trace = BuildNeighborGraphSequence(
            ReplayScenarioKind.MovingNeighborGraphDriftSuppressed,
            DeviceMotionState.Moving,
            MovingNeighborGraphDriftFrames,
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.MovingNeighborGraphDriftSuppressed,
            "Moving neighbor graph drift suppressed",
            "Moving context suppresses large neighbor graph changes.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("moving-neighbor-graph-drift-suppressed", TrustedNeighborGraphCells),
            HasNeighborGraphExpectation: true,
            ExpectedNeighborGraphSuppression: true,
            MaximumNeighborGraphScore: 0.1D,
            ExpectedNeighborGraphSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateMissingNeighborCellsSuppressed()
    {
        ReplayTrace trace = BuildServingSequence(
            ReplayScenarioKind.MissingNeighborCellsSuppressed,
            DeviceMotionState.Stationary,
            MissingNeighborServingSequence,
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.MissingNeighborCellsSuppressed,
            "Missing neighbor cells suppressed",
            "Stationary observations have serving cells but no neighbor cells.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("missing-neighbor-cells-suppressed", TrustedNeighborGraphCells),
            HasNeighborGraphExpectation: true,
            ExpectedNeighborGraphSuppression: true,
            MaximumNeighborGraphScore: 0.1D,
            ExpectedNeighborGraphSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateInsufficientNeighborBaselineSuppressed()
    {
        ReplayTrace trace = BuildNeighborGraphSequence(
            ReplayScenarioKind.InsufficientNeighborBaselineSuppressed,
            DeviceMotionState.Stationary,
            RepeatedSevereNeighborGraphDriftFrames,
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.InsufficientNeighborBaselineSuppressed,
            "Insufficient neighbor baseline suppressed",
            "Neighbor graph drift is suppressed when the local neighbor baseline is insufficient.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("insufficient-neighbor-baseline-suppressed", "synthetic-neighbor-a"),
            HasNeighborGraphExpectation: true,
            ExpectedNeighborGraphSuppression: true,
            MaximumNeighborGraphScore: 0.1D,
            ExpectedNeighborGraphSeverity: RiskSeverity.None);
    }

    private static ReplayScenario CreateServingChangeWithGraphDrift()
    {
        ReplayTrace trace = BuildNeighborGraphSequence(
            ReplayScenarioKind.ServingChangeWithGraphDrift,
            DeviceMotionState.Stationary,
            ServingChangeWithGraphDriftFrames,
            TimeSpan.FromSeconds(30D));

        return new ReplayScenario(
            ReplayScenarioKind.ServingChangeWithGraphDrift,
            "Serving change with graph drift",
            "Stationary serving-cell changes occur together with large neighbor graph drift.",
            trace,
            ExpectedStationaryHandoverSuppression: false,
            HasStationaryHandoverExpectation: false,
            Baseline: CreateBaseline("serving-change-with-graph-drift", TrustedNeighborGraphCells),
            HasNeighborGraphExpectation: true,
            ExpectedNeighborGraphSuppression: false,
            MinimumNeighborGraphScore: 0.8D,
            ExpectedNeighborGraphSeverity: RiskSeverity.High);
    }

    private static ReplayTrace BuildServingSequence(
        ReplayScenarioKind kind,
        DeviceMotionState motionState,
        string[] syntheticCellIds,
        TimeSpan step)
    {
        var builder = new RadioObservationScenarioBuilder(kind);

        for (int index = 0; index < syntheticCellIds.Length; index++)
        {
            builder.AddServingCell(
                TimeSpan.FromTicks(step.Ticks * index),
                motionState,
                syntheticCellIds[index]);
        }

        return builder.Build();
    }

    private static ReplayTrace BuildRatSequence(
        ReplayScenarioKind kind,
        DeviceMotionState motionState,
        IReadOnlyList<RadioAccessTechnology> radioAccessTechnologies,
        TimeSpan step)
    {
        var builder = new RadioObservationScenarioBuilder(kind);

        for (int index = 0; index < radioAccessTechnologies.Count; index++)
        {
            builder.AddServingCell(
                TimeSpan.FromTicks(step.Ticks * index),
                motionState,
                "synthetic-rat-cell",
                radioAccessTechnology: radioAccessTechnologies[index]);
        }

        return builder.Build();
    }

    private static ReplayTrace BuildNeighborGraphSequence(
        ReplayScenarioKind kind,
        DeviceMotionState motionState,
        (string ServingCellId, string[] NeighborCellIds)[] frames,
        TimeSpan step)
    {
        var builder = new RadioObservationScenarioBuilder(kind);

        for (int index = 0; index < frames.Length; index++)
        {
            builder.AddServingCellWithNeighbors(
                TimeSpan.FromTicks(step.Ticks * index),
                motionState,
                frames[index].ServingCellId,
                frames[index].NeighborCellIds);
        }

        return builder.Build();
    }

    private static BaselineProfile CreateBaseline(string profileId, params string[] syntheticCellIds)
    {
        TrustedCellSignature[] trustedCells = syntheticCellIds
            .Select(static syntheticCellId => new TrustedCellSignature(
                string.Create(CultureInfo.InvariantCulture, $"trusted:{syntheticCellId}"),
                RadioAccessTechnology.Lte,
                NetworkCellPseudonym: syntheticCellId,
                ChannelNumber: 6300))
            .ToArray();
        DateTimeOffset createdAt = RadioObservationScenarioBuilder.BaseTimestamp.AddDays(-7D);

        return new BaselineProfile(profileId, createdAt, RadioObservationScenarioBuilder.BaseTimestamp, trustedCells);
    }
}
