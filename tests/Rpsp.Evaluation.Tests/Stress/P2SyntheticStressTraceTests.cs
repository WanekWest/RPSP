using Rpsp.Core.Observations;
using Rpsp.Evaluation.Stress;
using Xunit;

namespace Rpsp.Evaluation.Tests.Stress;

public sealed class P2SyntheticStressTraceTests
{
    private static readonly SyntheticStressTraceKind[] P2Kinds =
    [
        SyntheticStressTraceKind.LongMixedUrbanDayStress,
        SyntheticStressTraceKind.SparseEvidenceAmbiguousStress,
        SyntheticStressTraceKind.AlternatingBenignAndSuspiciousWindowsStress,
        SyntheticStressTraceKind.BaselineWarmupThenUnknownCellStress,
        SyntheticStressTraceKind.NeighborGraphPartialRecoveryStress
    ];

    [Fact]
    public void P2StressTracesExistAndAreDeterministic()
    {
        foreach (SyntheticStressTraceKind kind in P2Kinds)
        {
            SyntheticStressTrace first = SyntheticStressTraceCatalog.Get(kind);
            SyntheticStressTrace second = SyntheticStressTraceCatalog.Get(kind);

            Assert.NotEmpty(first.Trace.Frames);
            Assert.NotEmpty(first.Windows);
            Assert.Equal(CreateSignature(first), CreateSignature(second));
        }
    }

    [Fact]
    public void LongMixedUrbanDayTraceRemainsBounded()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(SyntheticStressTraceKind.LongMixedUrbanDayStress);

        Assert.Equal(36, trace.Trace.Frames.Count);
        Assert.InRange(trace.Trace.Frames.Count, 1, 48);
        Assert.Equal(
            ["morning-stable", "midday-mobility", "focused-burst", "evening-recovery"],
            trace.Windows.Select(static window => window.Name));
    }

    [Fact]
    public async Task SparseEvidenceAmbiguousStressDoesNotBecomeHighRisk()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(SyntheticStressTraceKind.SparseEvidenceAmbiguousStress);
        StressAssessment assessment = StressTraceTestSupport.Assess(
            await StressTraceTestSupport.CollectAsync(trace).ConfigureAwait(true),
            trace);

        Assert.True(assessment.Stationary.IsSuppressed);
        Assert.True(assessment.Unknown.IsSuppressed);
        Assert.True(assessment.Neighbor.IsSuppressed);
        Assert.True(assessment.Composite.RiskScore.Value <= 0.35D);
    }

    [Fact]
    public async Task AlternatingTraceProducesHighRiskOnlyInSuspiciousWindows()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(
            SyntheticStressTraceKind.AlternatingBenignAndSuspiciousWindowsStress);
        IReadOnlyList<RadioObservation> observations = await StressTraceTestSupport
            .CollectAsync(trace)
            .ConfigureAwait(true);

        StressAssessment benignA = AssessWindow(trace, observations, "benign-a");
        StressAssessment suspiciousA = AssessWindow(trace, observations, "suspicious-a");
        StressAssessment benignB = AssessWindow(trace, observations, "benign-b");
        StressAssessment suspiciousB = AssessWindow(trace, observations, "suspicious-b");
        StressAssessment recovery = AssessWindow(trace, observations, "recovery");

        Assert.True(benignA.Composite.RiskScore.Value <= 0.25D);
        Assert.True(suspiciousA.Composite.RiskScore.Value >= 0.8D);
        Assert.True(benignB.Composite.RiskScore.Value <= 0.25D);
        Assert.True(suspiciousB.Composite.RiskScore.Value >= 0.8D);
        Assert.True(recovery.Composite.RiskScore.Value <= 0.35D);
    }

    [Fact]
    public async Task BaselineWarmupTraceSuppressesEarlyWindowsBeforeUnknownCellRisk()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(
            SyntheticStressTraceKind.BaselineWarmupThenUnknownCellStress);
        IReadOnlyList<RadioObservation> observations = await StressTraceTestSupport
            .CollectAsync(trace)
            .ConfigureAwait(true);

        StressAssessment early = AssessWindow(trace, observations, "early-warmup");
        StressAssessment trustedWarm = AssessWindow(trace, observations, "trusted-warm");
        StressAssessment unknown = AssessWindow(trace, observations, "unknown-after-warmup");

        Assert.True(early.Stationary.IsSuppressed);
        Assert.True(early.Unknown.IsSuppressed);
        Assert.True(early.Neighbor.IsSuppressed);
        Assert.True(trustedWarm.Composite.RiskScore.Value <= 0.25D);
        Assert.True(unknown.Unknown.RiskScore.Value >= 0.8D);
        Assert.True(unknown.Composite.RiskScore.Value >= 0.8D);
    }

    [Fact]
    public async Task NeighborGraphPartialRecoveryReducesRiskAfterSimilarityRecovers()
    {
        SyntheticStressTrace trace = SyntheticStressTraceCatalog.Get(
            SyntheticStressTraceKind.NeighborGraphPartialRecoveryStress);
        IReadOnlyList<RadioObservation> observations = await StressTraceTestSupport
            .CollectAsync(trace)
            .ConfigureAwait(true);

        StressAssessment stable = AssessWindow(trace, observations, "stable-before-drift");
        StressAssessment severe = AssessWindow(trace, observations, "severe-drift");
        StressAssessment recovery = AssessWindow(trace, observations, "partial-recovery");

        Assert.True(stable.Neighbor.RiskScore.Value <= 0.25D);
        Assert.True(severe.Neighbor.RiskScore.Value >= 0.8D);
        Assert.True(recovery.Neighbor.RiskScore.Value < severe.Neighbor.RiskScore.Value);
        Assert.True(recovery.Composite.RiskScore.Value < severe.Composite.RiskScore.Value);
    }

    private static StressAssessment AssessWindow(
        SyntheticStressTrace trace,
        IReadOnlyList<RadioObservation> observations,
        string windowName)
    {
        return StressTraceTestSupport.Assess(
            StressTraceTestSupport.Window(
                observations,
                StressTraceTestSupport.RequireWindow(trace, windowName)),
            trace);
    }

    private static string CreateSignature(SyntheticStressTrace trace)
    {
        return string.Join(
            '|',
            trace.Trace.Frames.Select(static frame =>
            {
                string cells = string.Join(
                    ',',
                    frame.Observation.CellSnapshots.Select(static cell =>
                        string.Join(
                            ':',
                            cell.Role,
                            cell.RadioAccessTechnology,
                            cell.Identifier?.NetworkCellId ?? "none",
                            cell.Signal?.RsrpDbm?.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) ?? "na")));

                return $"{frame.Timestamp:O}:{cells}";
            }));
    }
}
