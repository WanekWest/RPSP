using System.Runtime.CompilerServices;
using Rpsp.Core.Abstractions;
using Rpsp.Core.Baselines;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Pipeline;
using Rpsp.Core.Reporting;
using Xunit;

namespace Rpsp.Core.Tests.Pipeline;

public sealed class RpspDetectionPipelineTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StableReplayScenarioProducesLowComposite()
    {
        var pipeline = new RpspDetectionPipeline();

        RpspDetectionPipelineResult result = pipeline.Run(
            CreateStableTrace(),
            CreateBaseline("known-a", "known-b", "known-c"));

        Assert.Equal(WarningLevel.None, result.CompositeAssessment.WarningDecision.Level);
        Assert.True(result.CompositeAssessment.RiskScore.Value < 0.2D);
        Assert.Equal(4, result.DetectorContributions.Count);
    }

    [Fact]
    public void SuspiciousMixedScenarioProducesHighComposite()
    {
        var pipeline = new RpspDetectionPipeline();

        RpspDetectionPipelineResult result = pipeline.Run(
            CreateSuspiciousMixedTrace(),
            CreateBaseline("known-a", "known-b", "known-c"));

        Assert.True(result.CompositeAssessment.RiskScore.Value >= 0.8D);
        Assert.Equal(WarningLevel.Warning, result.CompositeAssessment.WarningDecision.Level);
        Assert.Equal(4, result.DetectorContributions.Count);
    }

    [Fact]
    public void SuppressedMovingScenarioRemainsLow()
    {
        var pipeline = new RpspDetectionPipeline();

        RpspDetectionPipelineResult result = pipeline.Run(
            CreateMovingChurnTrace(),
            CreateBaseline("known-a", "known-b", "known-c"));

        Assert.True(result.CompositeAssessment.RiskScore.Value < 0.6D);
        Assert.Contains(result.DetectorContributions, static contribution => contribution.IsSuppressed);
    }

    [Fact]
    public void PipelineIncludesAllFourDetectorContributions()
    {
        var pipeline = new RpspDetectionPipeline();

        RpspDetectionPipelineResult result = pipeline.Run(
            CreateStableTrace(),
            CreateBaseline("known-a", "known-b", "known-c"));

        string[] detectorNames = result.DetectorContributions
            .Select(static contribution => contribution.DetectorName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "NeighborGraphAnomalyDetector",
                "RatDowngradeRiskDetector",
                "StationaryHandoverAnomalyDetector",
                "UnknownStrongCellDetector"
            ],
            detectorNames);
    }

    [Fact]
    public void PipelineReportIsDeterministic()
    {
        var pipeline = new RpspDetectionPipeline();
        var formatter = new PlainTextDetectorReportFormatter();

        RpspDetectionPipelineResult first = pipeline.Run(
            CreateSuspiciousMixedTrace(),
            CreateBaseline("known-a", "known-b", "known-c"));
        RpspDetectionPipelineResult second = pipeline.Run(
            CreateSuspiciousMixedTrace(),
            CreateBaseline("known-a", "known-b", "known-c"));

        Assert.Equal(
            formatter.Format(first.CompositeReport),
            formatter.Format(second.CompositeReport));
    }

    [Fact]
    public async Task AsyncPipelineUsesObservationSourceAndPrivacyTransformer()
    {
        RadioObservation[] observations = CreateStableTrace();
        var observationSource = new ReplayObservationSource(observations);
        var privacyTransformer = new CountingPrivacyTransformer();
        var pipeline = new RpspDetectionPipeline();

        RpspDetectionPipelineResult result = await pipeline.RunAsync(
            observationSource,
            CreateBaseline("known-a", "known-b", "known-c"),
            privacyTransformer).ConfigureAwait(true);

        Assert.Equal(observations.Length, result.Observations.Count);
        Assert.Equal(observations.Length, privacyTransformer.TransformCount);
        Assert.Equal(WarningLevel.None, result.CompositeAssessment.WarningDecision.Level);
    }

    [Fact]
    public async Task AsyncPipelineHonorsCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync().ConfigureAwait(true);
        var pipeline = new RpspDetectionPipeline();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await pipeline.RunAsync(
                new ReplayObservationSource(CreateStableTrace()),
                CreateBaseline("known-a", "known-b", "known-c"),
                cancellationToken: cancellationSource.Token).ConfigureAwait(true)).ConfigureAwait(true);
    }

    [Fact]
    public void PipelineReferencesOnlyCoreAssembly()
    {
        string platformAdapterAssembly = string.Concat("Rpsp.", "And", "roid");
        string persistenceAssembly = "Rpsp.Persistence";

        string[] references = typeof(RpspDetectionPipeline).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(platformAdapterAssembly, references);
        Assert.DoesNotContain(persistenceAssembly, references);
    }

    private static BaselineProfile CreateBaseline(params string[] cellIds)
    {
        TrustedCellSignature[] trustedCells = cellIds
            .Select(static cellId => new TrustedCellSignature(
                string.Create(null, $"trusted:{cellId}"),
                RadioAccessTechnology.Lte,
                NetworkCellPseudonym: cellId,
                ChannelNumber: 6300))
            .ToArray();

        return new BaselineProfile("pipeline-baseline", Start.AddDays(-7D), Start, trustedCells);
    }

    private static RadioObservation[] CreateStableTrace()
    {
        return Enumerable.Range(0, 6)
            .Select(static index => CreateObservation(
                string.Create(null, $"stable-{index}"),
                Start.AddSeconds(index * 30D),
                DeviceMotionState.Stationary,
                RadioAccessTechnology.Lte,
                "known-a",
                -86D,
                ["known-b", "known-c"]))
            .ToArray();
    }

    private static RadioObservation[] CreateSuspiciousMixedTrace()
    {
        return
        [
            CreateObservation("mixed-0", Start, DeviceMotionState.Stationary, RadioAccessTechnology.Lte, "known-a", -84D, ["known-b", "known-c"]),
            CreateObservation("mixed-1", Start.AddSeconds(30D), DeviceMotionState.Stationary, RadioAccessTechnology.Nr, "known-b", -85D, ["known-a", "known-c"]),
            CreateObservation("mixed-2", Start.AddSeconds(60D), DeviceMotionState.Stationary, RadioAccessTechnology.Gsm, "unknown-a", -63D, ["unknown-n1", "unknown-n2"]),
            CreateObservation("mixed-3", Start.AddSeconds(90D), DeviceMotionState.Stationary, RadioAccessTechnology.Gsm, "unknown-b", -62D, ["unknown-n2", "unknown-n3"]),
            CreateObservation("mixed-4", Start.AddSeconds(120D), DeviceMotionState.Stationary, RadioAccessTechnology.Umts, "unknown-c", -64D, ["unknown-n3", "unknown-n4"]),
            CreateObservation("mixed-5", Start.AddSeconds(150D), DeviceMotionState.Stationary, RadioAccessTechnology.Gsm, "unknown-d", -61D, ["unknown-n4", "unknown-n5"]),
            CreateObservation("mixed-6", Start.AddSeconds(180D), DeviceMotionState.Stationary, RadioAccessTechnology.Gsm, "unknown-e", -62D, ["unknown-n5", "unknown-n6"]),
            CreateObservation("mixed-7", Start.AddSeconds(210D), DeviceMotionState.Stationary, RadioAccessTechnology.Gsm, "unknown-f", -63D, ["unknown-n6", "unknown-n7"])
        ];
    }

    private static RadioObservation[] CreateMovingChurnTrace()
    {
        string[] servingCells = ["known-a", "unknown-a", "known-b", "unknown-b", "known-c", "unknown-c"];

        return servingCells
            .Select((cellId, index) => CreateObservation(
                string.Create(null, $"moving-{index}"),
                Start.AddSeconds(index * 30D),
                DeviceMotionState.InVehicle,
                RadioAccessTechnology.Lte,
                cellId,
                -72D,
                ["known-b", "known-c"]))
            .ToArray();
    }

    private static RadioObservation CreateObservation(
        string observationId,
        DateTimeOffset observedAt,
        DeviceMotionState motionState,
        RadioAccessTechnology radioAccessTechnology,
        string servingCellId,
        double servingRsrpDbm,
        IReadOnlyList<string> neighborCellIds)
    {
        var cells = new List<CellSnapshot>
        {
            CreateCell(CellRole.Serving, radioAccessTechnology, servingCellId, servingRsrpDbm, isRegistered: true)
        };

        foreach (string neighborCellId in neighborCellIds)
        {
            cells.Add(CreateCell(
                CellRole.Neighbor,
                radioAccessTechnology,
                neighborCellId,
                servingRsrpDbm - 4D,
                isRegistered: false));
        }

        var serviceState = new ServiceStateSnapshot(
            IsInService: true,
            VoiceRadioAccessTechnology: radioAccessTechnology,
            DataRadioAccessTechnology: radioAccessTechnology);
        var deviceMotion = new DeviceMotionSnapshot(motionState, confidence: 0.95D);

        return new RadioObservation(
            observationId,
            observedAt,
            ObservationSourceKind.Replay,
            cells,
            serviceState,
            deviceMotion: deviceMotion);
    }

    private static CellSnapshot CreateCell(
        CellRole role,
        RadioAccessTechnology radioAccessTechnology,
        string cellId,
        double rsrpDbm,
        bool isRegistered)
    {
        return new CellSnapshot(
            role,
            radioAccessTechnology,
            new CellIdentifier(new Plmn("001", "01"), cellId, ChannelNumber: 6300),
            new SignalSnapshot(RsrpDbm: rsrpDbm),
            ObservationAvailability.Available,
            IsRegistered: isRegistered);
    }

    private sealed class ReplayObservationSource(IReadOnlyList<RadioObservation> observations) : IObservationSource
    {
        public async IAsyncEnumerable<RadioObservation> ObserveAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (RadioObservation observation in observations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return observation;
            }
        }
    }

    private sealed class CountingPrivacyTransformer : IPrivacyTransformer
    {
        public int TransformCount { get; private set; }

        public ValueTask<RadioObservation> TransformAsync(
            RadioObservation observation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TransformCount++;

            return ValueTask.FromResult(observation);
        }
    }
}
