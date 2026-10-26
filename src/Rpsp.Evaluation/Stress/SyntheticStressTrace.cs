using Rpsp.Core.Baselines;

namespace Rpsp.Evaluation.Stress;

public sealed record SyntheticStressTrace(
    SyntheticStressTraceKind Kind,
    string Name,
    string Description,
    ReplayTrace Trace,
    BaselineProfile Baseline,
    SyntheticStressExpectation Expectation,
    IReadOnlyList<SyntheticStressWindow> Windows);

public sealed record SyntheticStressWindow(
    string Name,
    int StartIndex,
    int ObservationCount);
