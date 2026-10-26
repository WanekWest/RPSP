using Rpsp.Core.Observations;

namespace Rpsp.Evaluation;

public sealed record TraceFrame(DateTimeOffset Timestamp, RadioObservation Observation);
