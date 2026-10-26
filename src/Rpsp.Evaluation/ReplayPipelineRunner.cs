using Rpsp.Core.Baselines;
using Rpsp.Core.Observations;
using Rpsp.Core.Pipeline;
using Rpsp.Core.Reporting;
using Rpsp.Core.Scoring;
using Rpsp.Evaluation.Stress;

namespace Rpsp.Evaluation;

public enum ReplayPipelineWindowMode
{
    FullTrace = 0,
    Sliding = 1,
    DeclaredStressWindows = 2
}

public sealed record ReplayPipelineWindowStrategy
{
    private ReplayPipelineWindowStrategy(
        ReplayPipelineWindowMode mode,
        int? windowSize = null,
        int step = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(step, 1);

        if (mode == ReplayPipelineWindowMode.Sliding)
        {
            if (windowSize is null)
            {
                throw new ArgumentNullException(nameof(windowSize));
            }

            ArgumentOutOfRangeException.ThrowIfLessThan(windowSize.Value, 1);
        }

        Mode = mode;
        WindowSize = windowSize;
        Step = step;
    }

    public ReplayPipelineWindowMode Mode { get; }

    public int? WindowSize { get; }

    public int Step { get; }

    public static ReplayPipelineWindowStrategy FullTrace { get; } =
        new(ReplayPipelineWindowMode.FullTrace);

    public static ReplayPipelineWindowStrategy DeclaredStressWindows { get; } =
        new(ReplayPipelineWindowMode.DeclaredStressWindows);

    public static ReplayPipelineWindowStrategy Sliding(int windowSize, int step = 1)
    {
        return new ReplayPipelineWindowStrategy(
            ReplayPipelineWindowMode.Sliding,
            windowSize,
            step);
    }
}

public sealed record ReplayPipelineWindowResult(
    string WindowName,
    int StartIndex,
    IReadOnlyList<RadioObservation> Observations,
    RpspDetectionPipelineResult PipelineResult)
{
    public int ObservationCount => Observations.Count;

    public CompositeRiskAssessment CompositeAssessment => PipelineResult.CompositeAssessment;

    public DetectorReport CompositeReport => PipelineResult.CompositeReport;

    public IReadOnlyList<DetectorReport> DetectorReports => PipelineResult.DetectorReports;
}

public sealed record ReplayPipelineRunResult(
    IReadOnlyList<ReplayPipelineWindowResult> Windows)
{
    public IReadOnlyList<CompositeRiskAssessment> CompositeAssessments =>
        Windows.Select(static window => window.CompositeAssessment).ToArray();

    public IReadOnlyList<DetectorReport> Reports =>
        Windows.Select(static window => window.CompositeReport).ToArray();
}

public static class ReplayPipelineRunner
{
    public static ReplayPipelineRunResult Run(
        ReplayTrace trace,
        RpspDetectionPipeline? pipeline = null,
        BaselineProfile? baseline = null,
        ReplayPipelineWindowStrategy? windowStrategy = null)
    {
        ArgumentNullException.ThrowIfNull(trace);

        return RunCore(
            trace,
            pipeline ?? new RpspDetectionPipeline(),
            baseline,
            windowStrategy ?? ReplayPipelineWindowStrategy.FullTrace,
            declaredWindows: []);
    }

    public static ReplayPipelineRunResult Run(
        SyntheticStressTrace trace,
        RpspDetectionPipeline? pipeline = null,
        BaselineProfile? baseline = null,
        ReplayPipelineWindowStrategy? windowStrategy = null)
    {
        ArgumentNullException.ThrowIfNull(trace);

        ReplayPipelineWindowStrategy effectiveStrategy = windowStrategy
            ?? (trace.Windows.Count == 0
                ? ReplayPipelineWindowStrategy.FullTrace
                : ReplayPipelineWindowStrategy.DeclaredStressWindows);

        return RunCore(
            trace.Trace,
            pipeline ?? new RpspDetectionPipeline(),
            baseline ?? trace.Baseline,
            effectiveStrategy,
            trace.Windows);
    }

    private static ReplayPipelineRunResult RunCore(
        ReplayTrace trace,
        RpspDetectionPipeline pipeline,
        BaselineProfile? baseline,
        ReplayPipelineWindowStrategy windowStrategy,
        IReadOnlyList<SyntheticStressWindow> declaredWindows)
    {
        RadioObservation[] observations = trace.Frames
            .Select(static frame => frame.Observation)
            .ToArray();
        ReplayPipelineWindow[] windows = CreateWindows(
            observations,
            windowStrategy,
            declaredWindows);
        ReplayPipelineWindowResult[] results = windows
            .Select(window => RunWindow(window, observations, pipeline, baseline))
            .ToArray();

        return new ReplayPipelineRunResult(results);
    }

    private static ReplayPipelineWindowResult RunWindow(
        ReplayPipelineWindow window,
        RadioObservation[] observations,
        RpspDetectionPipeline pipeline,
        BaselineProfile? baseline)
    {
        RadioObservation[] windowObservations = observations
            .Skip(window.StartIndex)
            .Take(window.ObservationCount)
            .ToArray();
        RpspDetectionPipelineResult pipelineResult = pipeline.Run(windowObservations, baseline);

        return new ReplayPipelineWindowResult(
            window.Name,
            window.StartIndex,
            windowObservations,
            pipelineResult);
    }

    private static ReplayPipelineWindow[] CreateWindows(
        RadioObservation[] observations,
        ReplayPipelineWindowStrategy strategy,
        IReadOnlyList<SyntheticStressWindow> declaredWindows)
    {
        return strategy.Mode switch
        {
            ReplayPipelineWindowMode.FullTrace =>
            [
                new ReplayPipelineWindow("full-trace", 0, observations.Length)
            ],
            ReplayPipelineWindowMode.DeclaredStressWindows => CreateDeclaredWindows(observations, declaredWindows),
            ReplayPipelineWindowMode.Sliding => CreateSlidingWindows(observations, strategy),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy.Mode, "Unknown window mode.")
        };
    }

    private static ReplayPipelineWindow[] CreateDeclaredWindows(
        RadioObservation[] observations,
        IReadOnlyList<SyntheticStressWindow> declaredWindows)
    {
        if (declaredWindows.Count == 0)
        {
            return [new ReplayPipelineWindow("full-trace", 0, observations.Length)];
        }

        return declaredWindows
            .Select(window => new ReplayPipelineWindow(
                window.Name,
                window.StartIndex,
                Math.Max(0, Math.Min(window.ObservationCount, observations.Length - window.StartIndex))))
            .ToArray();
    }

    private static ReplayPipelineWindow[] CreateSlidingWindows(
        RadioObservation[] observations,
        ReplayPipelineWindowStrategy strategy)
    {
        int windowSize = strategy.WindowSize ?? throw new InvalidOperationException(
            "Sliding window strategy requires a window size.");
        var windows = new List<ReplayPipelineWindow>();

        for (int start = 0; start + windowSize <= observations.Length; start += strategy.Step)
        {
            windows.Add(new ReplayPipelineWindow(
                $"sliding-{start}",
                start,
                windowSize));
        }

        return windows.ToArray();
    }
}

internal sealed record ReplayPipelineWindow(
    string Name,
    int StartIndex,
    int ObservationCount);
