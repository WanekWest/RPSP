using Rpsp.Core.Observations;

namespace Rpsp.Evaluation.Experiments;

public static class ExperimentTraceTransformer
{
    private const double StrongRsrpReferenceDbm = -80D;

    public static ReplayTrace Transform(
        ReplayTrace trace,
        double boundaryScale,
        int seed,
        double maximumSignalNoiseDb,
        double maximumTimingJitterSeconds)
    {
        ArgumentNullException.ThrowIfNull(trace);

        if (!double.IsFinite(boundaryScale) || boundaryScale <= 0D)
        {
            throw new ArgumentOutOfRangeException(nameof(boundaryScale));
        }

        if (!double.IsFinite(maximumSignalNoiseDb) || maximumSignalNoiseDb < 0D)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSignalNoiseDb));
        }

        if (!double.IsFinite(maximumTimingJitterSeconds) || maximumTimingJitterSeconds < 0D)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTimingJitterSeconds));
        }

        if (trace.Frames.Count == 0)
        {
            return trace;
        }

        var noise = new DeterministicNoiseGenerator(seed);
        DateTimeOffset origin = trace.Frames[0].Timestamp;
        DateTimeOffset previous = origin.AddTicks(-1L);
        var transformedFrames = new List<TraceFrame>(trace.Frames.Count);

        foreach (TraceFrame frame in trace.Frames)
        {
            TimeSpan originalOffset = frame.Timestamp - origin;
            double scaledSeconds = originalOffset.TotalSeconds / boundaryScale;
            double jitterSeconds = originalOffset == TimeSpan.Zero
                ? 0D
                : noise.NextSymmetric(maximumTimingJitterSeconds);
            DateTimeOffset timestamp = origin.AddSeconds(Math.Max(0D, scaledSeconds + jitterSeconds));

            if (timestamp <= previous)
            {
                timestamp = previous.AddMilliseconds(1D);
            }

            previous = timestamp;
            RadioObservation observation = TransformObservation(
                frame.Observation,
                timestamp,
                boundaryScale,
                maximumSignalNoiseDb,
                noise);
            transformedFrames.Add(new TraceFrame(timestamp, observation));
        }

        return new ReplayTrace(transformedFrames);
    }

    private static RadioObservation TransformObservation(
        RadioObservation observation,
        DateTimeOffset timestamp,
        double boundaryScale,
        double maximumSignalNoiseDb,
        DeterministicNoiseGenerator noise)
    {
        CellSnapshot[] cells = observation.CellSnapshots
            .Select(cell => TransformCell(cell, boundaryScale, maximumSignalNoiseDb, noise))
            .ToArray();

        return new RadioObservation(
            observation.ObservationId,
            timestamp,
            observation.SourceKind,
            cells,
            observation.ServiceState,
            observation.PhysicalChannels,
            observation.DeviceMotion);
    }

    private static CellSnapshot TransformCell(
        CellSnapshot cell,
        double boundaryScale,
        double maximumSignalNoiseDb,
        DeterministicNoiseGenerator noise)
    {
        if (cell.Signal is null)
        {
            return cell;
        }

        double? rsrp = cell.Signal.RsrpDbm is double value
            ? ClampSignal(
                StrongRsrpReferenceDbm + ((value - StrongRsrpReferenceDbm) * boundaryScale)
                + noise.NextSymmetric(maximumSignalNoiseDb))
            : null;

        SignalSnapshot signal = cell.Signal with { RsrpDbm = rsrp };
        return cell with { Signal = signal };
    }

    private static double ClampSignal(double value)
    {
        return Math.Clamp(value, -140D, -20D);
    }

    private sealed class DeterministicNoiseGenerator
    {
        private uint _state;

        public DeterministicNoiseGenerator(int seed)
        {
            _state = unchecked((uint)seed);

            if (_state == 0U)
            {
                _state = 0x6D2B79F5U;
            }
        }

        public double NextSymmetric(double maximumAbsoluteValue)
        {
            if (maximumAbsoluteValue == 0D)
            {
                return 0D;
            }

            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            double unit = _state / (double)uint.MaxValue;
            return ((unit * 2D) - 1D) * maximumAbsoluteValue;
        }
    }
}
