namespace Rpsp.Evaluation;

public sealed record ReplayTrace
{
    public ReplayTrace(IEnumerable<TraceFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        Frames = frames.OrderBy(static frame => frame.Timestamp).ToArray();
    }

    public IReadOnlyList<TraceFrame> Frames { get; }
}
