using Rpsp.Core.Abstractions;
using Rpsp.Core.Baselines;
using Rpsp.Core.Observations;
using Rpsp.Core.Reporting;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Pipeline;

public sealed class RpspDetectionPipeline
{
    private readonly CompositeRiskScorer _compositeRiskScorer;
    private readonly IRpspDetectorRunner _detectorRunner;

    public RpspDetectionPipeline(
        RpspDetectionPipelineOptions? options = null,
        IRpspDetectorRunner? detectorRunner = null)
    {
        RpspDetectionPipelineOptions pipelineOptions = options ?? new RpspDetectionPipelineOptions();

        _detectorRunner = detectorRunner ?? new StockCoreDetectorRunner(pipelineOptions);
        _compositeRiskScorer = new CompositeRiskScorer(pipelineOptions.CompositeRiskScorerOptions);
    }

    public RpspDetectionPipelineResult Run(
        IReadOnlyList<RadioObservation> observations,
        BaselineProfile? baseline = null)
    {
        ArgumentNullException.ThrowIfNull(observations);

        RadioObservation[] orderedObservations = observations
            .OrderBy(static observation => observation.ObservedAt)
            .ToArray();
        BaselineProfile effectiveBaseline = baseline ?? CreateEmptyBaseline(orderedObservations);
        DetectorPipelineRunResult detectorRun = _detectorRunner.Run(orderedObservations, effectiveBaseline);
        CompositeRiskAssessment assessment = _compositeRiskScorer.Assess(detectorRun.Contributions);
        DetectorReport compositeReport = CompositeRiskReportFactory.Create(assessment);

        return new RpspDetectionPipelineResult(
            orderedObservations,
            effectiveBaseline,
            detectorRun.Contributions,
            assessment,
            compositeReport,
            detectorRun.DetectorReports);
    }

    public async ValueTask<RpspDetectionPipelineResult> RunAsync(
        IObservationSource observationSource,
        BaselineProfile? baseline = null,
        IPrivacyTransformer? privacyTransformer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observationSource);

        var observations = new List<RadioObservation>();

        await foreach (RadioObservation observation in observationSource
            .ObserveAsync(cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            RadioObservation transformedObservation = privacyTransformer is null
                ? observation
                : await privacyTransformer
                    .TransformAsync(observation, cancellationToken)
                    .ConfigureAwait(false);

            observations.Add(transformedObservation);
        }

        return Run(observations, baseline);
    }

    private static BaselineProfile CreateEmptyBaseline(RadioObservation[] observations)
    {
        DateTimeOffset observedAt = observations.Length == 0
            ? DateTimeOffset.UnixEpoch
            : observations[0].ObservedAt;

        return new BaselineProfile("pipeline-empty-baseline", observedAt, observedAt, []);
    }
}
