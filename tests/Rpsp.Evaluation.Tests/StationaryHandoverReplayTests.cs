using Rpsp.Core.Detectors;
using Rpsp.Core.Observations;
using Rpsp.Evaluation;
using Xunit;

namespace Rpsp.Evaluation.Tests;

public sealed class StationaryHandoverReplayTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReplaySourceCanFeedDetectorDeterministically()
    {
        RadioObservation[] unorderedObservations =
        [
            CreateObservation("obs-2", Start.AddSeconds(60D), "A"),
            CreateObservation("obs-0", Start, "A"),
            CreateObservation("obs-4", Start.AddSeconds(120D), "B"),
            CreateObservation("obs-1", Start.AddSeconds(30D), "B"),
            CreateObservation("obs-3", Start.AddSeconds(90D), "C")
        ];
        var trace = new ReplayTrace(unorderedObservations.Select(static observation =>
            new TraceFrame(observation.ObservedAt, observation)));
        var source = new InMemoryTraceReplaySource(trace);
        var detector = new StationaryHandoverAnomalyDetector();

        StationaryHandoverDetectionResult first = await detector.AnalyzeAsync(source.ReplayAsync());
        StationaryHandoverDetectionResult second = await detector.AnalyzeAsync(source.ReplayAsync());

        Assert.Equal(first.RiskScore, second.RiskScore);
        Assert.Equal(first.Evidence.BundleId, second.Evidence.BundleId);
        Assert.True(first.RiskScore.Value >= 0.6D);
    }

    private static RadioObservation CreateObservation(string observationId, DateTimeOffset observedAt, string cellId)
    {
        var serving = new CellSnapshot(
            CellRole.Serving,
            RadioAccessTechnology.Lte,
            new CellIdentifier(new Plmn("001", "01"), cellId, PhysicalCellId: 42),
            new SignalSnapshot(RsrpDbm: -82D),
            ObservationAvailability.Available,
            IsRegistered: true);
        var deviceMotion = new DeviceMotionSnapshot(DeviceMotionState.Stationary, confidence: 0.95D);

        return new RadioObservation(
            observationId,
            observedAt,
            ObservationSourceKind.Replay,
            [serving],
            deviceMotion: deviceMotion);
    }
}
