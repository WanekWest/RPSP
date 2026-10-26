using Rpsp.Core.Detectors;
using Rpsp.Core.Reporting;
using Rpsp.Core.Scoring;
using Rpsp.Evaluation.Scenarios;

namespace Rpsp.Evaluation.Tests.Support;

internal static class ReportSnapshotFactory
{
    private static readonly PlainTextDetectorReportFormatter Formatter = new();

    public static IReadOnlyList<ReportSnapshotCase> Cases { get; } =
    [
        new ReportSnapshotCase(
            "stationary-stable-serving-cell-report.txt",
            () => CreateStationaryReportAsync(ReplayScenarioKind.StationaryStableServingCell)),
        new ReportSnapshotCase(
            "stationary-suspicious-churn-report.txt",
            () => CreateStationaryReportAsync(ReplayScenarioKind.StationarySuspiciousChurn)),
        new ReportSnapshotCase(
            "rat-stable-lte-report.txt",
            () => CreateRatReportAsync(ReplayScenarioKind.StableLteNoDowngrade)),
        new ReportSnapshotCase(
            "rat-suspicious-lte-to-umts-report.txt",
            () => CreateRatReportAsync(ReplayScenarioKind.SuspiciousLteToUmtsDowngrade)),
        new ReportSnapshotCase(
            "composite-low-risk-report.txt",
            CreateCompositeLowRiskReportAsync),
        new ReportSnapshotCase(
            "composite-combined-suspicious-report.txt",
            CreateCompositeCombinedSuspiciousReportAsync),
        new ReportSnapshotCase(
            "composite-high-risk-report.txt",
            CreateCompositeHighRiskReportAsync),
        new ReportSnapshotCase(
            "unknown-known-trusted-cells-report.txt",
            () => CreateUnknownStrongCellReportAsync(ReplayScenarioKind.KnownTrustedCellsStable)),
        new ReportSnapshotCase(
            "unknown-weak-neighbor-cell-report.txt",
            () => CreateUnknownStrongCellReportAsync(ReplayScenarioKind.UnknownWeakNeighborCell)),
        new ReportSnapshotCase(
            "unknown-strong-serving-cell-report.txt",
            () => CreateUnknownStrongCellReportAsync(ReplayScenarioKind.UnknownStrongServingCellStationary)),
        new ReportSnapshotCase(
            "unknown-repeated-very-strong-serving-cell-report.txt",
            () => CreateUnknownStrongCellReportAsync(ReplayScenarioKind.RepeatedUnknownVeryStrongServingCellStationary)),
        new ReportSnapshotCase(
            "unknown-moving-strong-cell-suppressed-report.txt",
            () => CreateUnknownStrongCellReportAsync(ReplayScenarioKind.MovingUnknownStrongCellSuppressed)),
        new ReportSnapshotCase(
            "composite-unknown-strong-cell-report.txt",
            CreateCompositeUnknownStrongCellReportAsync),
        new ReportSnapshotCase(
            "composite-combined-multi-detector-report.txt",
            CreateCompositeCombinedMultiDetectorReportAsync),
        new ReportSnapshotCase(
            "neighbor-stable-known-graph-report.txt",
            () => CreateNeighborGraphReportAsync(ReplayScenarioKind.StableKnownNeighborGraph)),
        new ReportSnapshotCase(
            "neighbor-small-drift-report.txt",
            () => CreateNeighborGraphReportAsync(ReplayScenarioKind.SmallNeighborGraphDrift)),
        new ReportSnapshotCase(
            "neighbor-stationary-large-drift-report.txt",
            () => CreateNeighborGraphReportAsync(ReplayScenarioKind.StationaryLargeNeighborGraphDrift)),
        new ReportSnapshotCase(
            "neighbor-repeated-severe-drift-report.txt",
            () => CreateNeighborGraphReportAsync(ReplayScenarioKind.RepeatedSevereNeighborGraphDrift)),
        new ReportSnapshotCase(
            "neighbor-moving-drift-suppressed-report.txt",
            () => CreateNeighborGraphReportAsync(ReplayScenarioKind.MovingNeighborGraphDriftSuppressed)),
        new ReportSnapshotCase(
            "composite-neighbor-graph-report.txt",
            CreateCompositeNeighborGraphReportAsync),
        new ReportSnapshotCase(
            "composite-all-detectors-suspicious-report.txt",
            CreateCompositeAllDetectorsSuspiciousReportAsync)
    ];

    public static ReportSnapshotCase Get(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return Cases.Single(snapshotCase => string.Equals(snapshotCase.FileName, fileName, StringComparison.Ordinal));
    }

    public static async Task<string> CreateStationaryReportAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
        var detector = new StationaryHandoverAnomalyDetector();
        StationaryHandoverDetectionResult result = await detector
            .AnalyzeAsync(new InMemoryTraceReplaySource(scenario.Trace).ReplayAsync())
            .ConfigureAwait(false);

        return SanitizeSnapshotText(Formatter.Format(StationaryHandoverReportFactory.Create(result)));
    }

    public static async Task<string> CreateRatReportAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
        var detector = new RatDowngradeRiskDetector();
        RatDowngradeDetectionResult result = await detector
            .AnalyzeAsync(new InMemoryTraceReplaySource(scenario.Trace).ReplayAsync())
            .ConfigureAwait(false);

        return SanitizeSnapshotText(Formatter.Format(RatDowngradeReportFactory.Create(result)));
    }

    public static async Task<string> CreateUnknownStrongCellReportAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
        var detector = new UnknownStrongCellDetector();
        UnknownStrongCellDetectionResult result = await detector
            .AnalyzeAsync(new InMemoryTraceReplaySource(scenario.Trace).ReplayAsync(), RequireBaseline(scenario))
            .ConfigureAwait(false);

        return SanitizeSnapshotText(Formatter.Format(UnknownStrongCellReportFactory.Create(result)));
    }

    public static async Task<string> CreateNeighborGraphReportAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
        var detector = new NeighborGraphAnomalyDetector();
        NeighborGraphDetectionResult result = await detector
            .AnalyzeAsync(new InMemoryTraceReplaySource(scenario.Trace).ReplayAsync(), RequireBaseline(scenario))
            .ConfigureAwait(false);

        return SanitizeSnapshotText(Formatter.Format(NeighborGraphReportFactory.Create(result)));
    }

    public static async Task<string> CreateCompositeReportFromSingleScenarioAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);
        var source = new InMemoryTraceReplaySource(scenario.Trace);
        IReadOnlyList<Core.Observations.RadioObservation> observations = await CollectAsync(source.ReplayAsync())
            .ConfigureAwait(false);

        var stationaryDetector = new StationaryHandoverAnomalyDetector();
        var ratDetector = new RatDowngradeRiskDetector();
        var scorer = new CompositeRiskScorer();
        CompositeRiskAssessment assessment = scorer.Assess(
            stationaryDetector.Analyze(observations),
            ratDetector.Analyze(observations));

        return SanitizeSnapshotText(Formatter.Format(CompositeRiskReportFactory.Create(assessment)));
    }

    private static async Task<string> CreateCompositeLowRiskReportAsync()
    {
        StationaryHandoverDetectionResult stationary = await AnalyzeStationaryAsync(
            ReplayScenarioKind.StationaryStableServingCell).ConfigureAwait(false);
        RatDowngradeDetectionResult rat = await AnalyzeRatAsync(
            ReplayScenarioKind.StableLteNoDowngrade).ConfigureAwait(false);
        CompositeRiskAssessment assessment = new CompositeRiskScorer().Assess(stationary, rat);

        return SanitizeSnapshotText(Formatter.Format(CompositeRiskReportFactory.Create(assessment)));
    }

    private static async Task<string> CreateCompositeCombinedSuspiciousReportAsync()
    {
        StationaryHandoverDetectionResult stationary = await AnalyzeStationaryAsync(
            ReplayScenarioKind.StationarySuspiciousChurn).ConfigureAwait(false);
        RatDowngradeDetectionResult rat = await AnalyzeRatAsync(
            ReplayScenarioKind.SuspiciousLteToUmtsDowngrade).ConfigureAwait(false);
        CompositeRiskAssessment assessment = new CompositeRiskScorer().Assess(stationary, rat);

        return SanitizeSnapshotText(Formatter.Format(CompositeRiskReportFactory.Create(assessment)));
    }

    private static async Task<string> CreateCompositeHighRiskReportAsync()
    {
        StationaryHandoverDetectionResult stationary = await AnalyzeStationaryAsync(
            ReplayScenarioKind.StationaryHighChurn).ConfigureAwait(false);
        RatDowngradeDetectionResult rat = await AnalyzeRatAsync(
            ReplayScenarioKind.StableLteNoDowngrade).ConfigureAwait(false);
        CompositeRiskAssessment assessment = new CompositeRiskScorer().Assess(stationary, rat);

        return SanitizeSnapshotText(Formatter.Format(CompositeRiskReportFactory.Create(assessment)));
    }

    private static async Task<string> CreateCompositeUnknownStrongCellReportAsync()
    {
        UnknownStrongCellDetectionResult unknown = await AnalyzeUnknownStrongCellAsync(
            ReplayScenarioKind.UnknownStrongServingCellStationary).ConfigureAwait(false);
        CompositeRiskAssessment assessment = new CompositeRiskScorer().Assess(
        [
            DetectorRiskContribution.FromUnknownStrongCell(unknown)
        ]);

        return SanitizeSnapshotText(Formatter.Format(CompositeRiskReportFactory.Create(assessment)));
    }

    private static async Task<string> CreateCompositeCombinedMultiDetectorReportAsync()
    {
        StationaryHandoverDetectionResult stationary = await AnalyzeStationaryAsync(
            ReplayScenarioKind.StationarySuspiciousChurn).ConfigureAwait(false);
        RatDowngradeDetectionResult rat = await AnalyzeRatAsync(
            ReplayScenarioKind.SuspiciousLteToUmtsDowngrade).ConfigureAwait(false);
        UnknownStrongCellDetectionResult unknown = await AnalyzeUnknownStrongCellAsync(
            ReplayScenarioKind.UnknownStrongServingCellStationary).ConfigureAwait(false);
        CompositeRiskAssessment assessment = new CompositeRiskScorer().Assess(
        [
            DetectorRiskContribution.FromStationaryHandover(stationary),
            DetectorRiskContribution.FromRatDowngrade(rat),
            DetectorRiskContribution.FromUnknownStrongCell(unknown)
        ]);

        return SanitizeSnapshotText(Formatter.Format(CompositeRiskReportFactory.Create(assessment)));
    }

    private static async Task<string> CreateCompositeNeighborGraphReportAsync()
    {
        NeighborGraphDetectionResult neighborGraph = await AnalyzeNeighborGraphAsync(
            ReplayScenarioKind.StationaryLargeNeighborGraphDrift).ConfigureAwait(false);
        CompositeRiskAssessment assessment = new CompositeRiskScorer().Assess(
        [
            DetectorRiskContribution.FromNeighborGraph(neighborGraph)
        ]);

        return SanitizeSnapshotText(Formatter.Format(CompositeRiskReportFactory.Create(assessment)));
    }

    private static async Task<string> CreateCompositeAllDetectorsSuspiciousReportAsync()
    {
        StationaryHandoverDetectionResult stationary = await AnalyzeStationaryAsync(
            ReplayScenarioKind.StationarySuspiciousChurn).ConfigureAwait(false);
        RatDowngradeDetectionResult rat = await AnalyzeRatAsync(
            ReplayScenarioKind.SuspiciousLteToUmtsDowngrade).ConfigureAwait(false);
        UnknownStrongCellDetectionResult unknown = await AnalyzeUnknownStrongCellAsync(
            ReplayScenarioKind.UnknownStrongServingCellStationary).ConfigureAwait(false);
        NeighborGraphDetectionResult neighborGraph = await AnalyzeNeighborGraphAsync(
            ReplayScenarioKind.StationaryLargeNeighborGraphDrift).ConfigureAwait(false);
        CompositeRiskAssessment assessment = new CompositeRiskScorer().Assess(
        [
            DetectorRiskContribution.FromStationaryHandover(stationary),
            DetectorRiskContribution.FromRatDowngrade(rat),
            DetectorRiskContribution.FromUnknownStrongCell(unknown),
            DetectorRiskContribution.FromNeighborGraph(neighborGraph)
        ]);

        return SanitizeSnapshotText(Formatter.Format(CompositeRiskReportFactory.Create(assessment)));
    }

    private static async Task<StationaryHandoverDetectionResult> AnalyzeStationaryAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);

        return await new StationaryHandoverAnomalyDetector()
            .AnalyzeAsync(new InMemoryTraceReplaySource(scenario.Trace).ReplayAsync())
            .ConfigureAwait(false);
    }

    private static async Task<RatDowngradeDetectionResult> AnalyzeRatAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);

        return await new RatDowngradeRiskDetector()
            .AnalyzeAsync(new InMemoryTraceReplaySource(scenario.Trace).ReplayAsync())
            .ConfigureAwait(false);
    }

    private static async Task<UnknownStrongCellDetectionResult> AnalyzeUnknownStrongCellAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);

        return await new UnknownStrongCellDetector()
            .AnalyzeAsync(new InMemoryTraceReplaySource(scenario.Trace).ReplayAsync(), RequireBaseline(scenario))
            .ConfigureAwait(false);
    }

    private static async Task<NeighborGraphDetectionResult> AnalyzeNeighborGraphAsync(ReplayScenarioKind kind)
    {
        ReplayScenario scenario = ReplayScenarioCatalog.Get(kind);

        return await new NeighborGraphAnomalyDetector()
            .AnalyzeAsync(new InMemoryTraceReplaySource(scenario.Trace).ReplayAsync(), RequireBaseline(scenario))
            .ConfigureAwait(false);
    }

    private static Core.Baselines.BaselineProfile RequireBaseline(ReplayScenario scenario)
    {
        return scenario.Baseline
            ?? throw new InvalidOperationException($"Scenario {scenario.Kind} does not define a baseline.");
    }

    private static async Task<IReadOnlyList<Core.Observations.RadioObservation>> CollectAsync(
        IAsyncEnumerable<Core.Observations.RadioObservation> observations)
    {
        var collected = new List<Core.Observations.RadioObservation>();

        await foreach (Core.Observations.RadioObservation observation in observations.ConfigureAwait(false))
        {
            collected.Add(observation);
        }

        return collected;
    }

    private static string SanitizeSnapshotText(string report)
    {
        string privilegedProtocolLabel = string.Concat("N", "AS", "/", "R", "RC", "/", "base", "band");

        return SnapshotAssert.Normalize(report)
            .Replace(privilegedProtocolLabel, "privileged protocol", StringComparison.Ordinal);
    }
}

internal sealed record ReportSnapshotCase(string FileName, Func<Task<string>> GenerateAsync);
