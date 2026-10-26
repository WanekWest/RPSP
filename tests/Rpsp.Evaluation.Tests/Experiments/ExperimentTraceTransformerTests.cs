using Rpsp.Evaluation.Experiments;
using Rpsp.Evaluation.Scenarios;
using Xunit;

namespace Rpsp.Evaluation.Tests.Experiments;

public sealed class ExperimentTraceTransformerTests
{
    [Fact]
    public void TransformIsDeterministicAndPreservesFrameCount()
    {
        ReplayTrace source = ReplayScenarioCatalog.Get(ReplayScenarioKind.UnknownStrongServingCellStationary).Trace;

        ReplayTrace first = ExperimentTraceTransformer.Transform(source, 0.75D, 42, 1.5D, 2D);
        ReplayTrace second = ExperimentTraceTransformer.Transform(source, 0.75D, 42, 1.5D, 2D);

        Assert.Equal(source.Frames.Count, first.Frames.Count);
        Assert.Equal(
            first.Frames.Select(static frame => frame.Timestamp),
            second.Frames.Select(static frame => frame.Timestamp));
        Assert.Equal(
            first.Frames.SelectMany(static frame => frame.Observation.CellSnapshots).Select(static cell => cell.Signal?.RsrpDbm),
            second.Frames.SelectMany(static frame => frame.Observation.CellSnapshots).Select(static cell => cell.Signal?.RsrpDbm));
        Assert.True(first.Frames.Zip(first.Frames.Skip(1)).All(static pair => pair.First.Timestamp < pair.Second.Timestamp));
    }

    [Fact]
    public void TransformChangesSignalOrTimingWithoutChangingIdentifiers()
    {
        ReplayTrace source = ReplayScenarioCatalog.Get(ReplayScenarioKind.UnknownStrongServingCellStationary).Trace;
        ReplayTrace transformed = ExperimentTraceTransformer.Transform(source, 1.25D, 77, 1.5D, 2D);

        Assert.Equal(source.Frames[0].Observation.ObservationId, transformed.Frames[0].Observation.ObservationId);
        Assert.NotEqual(
            source.Frames[1].Observation.CellSnapshots[0].Signal?.RsrpDbm,
            transformed.Frames[1].Observation.CellSnapshots[0].Signal?.RsrpDbm);
    }
}
