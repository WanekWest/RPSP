using System.Diagnostics;
using Rpsp.Core.Events;
using Rpsp.Core.Pipeline;
using Rpsp.Core.Scoring;
using Rpsp.Evaluation.Scenarios;
using Rpsp.Evaluation.Stress;

namespace Rpsp.Evaluation.Experiments;

public static class ExperimentRunner
{
    private const string DataSource = "replay/synthetic-perturbation";

    public static ExperimentResultSet Run(ExperimentManifest manifest, RunProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(provenance);
        manifest.Validate();

        string expectedHash = ExperimentManifestSerializer.ComputeConfigurationHash(manifest);

        if (!string.Equals(provenance.ConfigurationHash, expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Run provenance does not match the experiment manifest hash.");
        }

        var records = new List<ExperimentResultRecord>();

        foreach (double boundaryScale in manifest.BoundaryScales)
        {
            for (int replicate = 0; replicate < manifest.Replicates; replicate++)
            {
                int seed = checked(manifest.BaseSeed + replicate);

                foreach (string ablationProfile in manifest.AblationProfiles)
                {
                    if (manifest.IncludeReplayScenarios)
                    {
                        AddReplayRecords(manifest, provenance, ablationProfile, boundaryScale, replicate, seed, records);
                    }

                    if (manifest.IncludeStressTraces)
                    {
                        AddStressRecords(manifest, provenance, ablationProfile, boundaryScale, replicate, seed, records);
                    }
                }
            }
        }

        return new ExperimentResultSet(manifest, provenance, records);
    }

    private static void AddReplayRecords(
        ExperimentManifest manifest,
        RunProvenance provenance,
        string ablationProfile,
        double boundaryScale,
        int replicate,
        int seed,
        List<ExperimentResultRecord> records)
    {
        foreach (ReplayScenario scenario in ReplayScenarioCatalog.All.OrderBy(static item => item.Kind))
        {
            ReplayTrace trace = ExperimentTraceTransformer.Transform(
                scenario.Trace,
                boundaryScale,
                CombineSeed(seed, (int)scenario.Kind, sourceSalt: 17),
                manifest.MaximumSignalNoiseDb,
                manifest.MaximumTimingJitterSeconds);

            long started = manifest.CaptureTiming ? Stopwatch.GetTimestamp() : 0L;
            ReplayPipelineRunResult result = ReplayPipelineRunner.Run(
                trace,
                ExperimentAblationProfiles.CreatePipeline(ablationProfile),
                scenario.Baseline,
                ReplayPipelineWindowStrategy.FullTrace);
            double elapsed = GetElapsedMilliseconds(manifest.CaptureTiming, started);
            string runId = CreateRunId(manifest.ExperimentId, "replay", scenario.Kind.ToString(), ablationProfile, boundaryScale, replicate, seed);

            AddWindowRecords(
                manifest,
                provenance,
                "replay",
                scenario.Kind.ToString(),
                ablationProfile,
                boundaryScale,
                replicate,
                seed,
                runId,
                result,
                elapsed,
                (detector, _) => GetReplayLabel(scenario, detector),
                records);
        }
    }

    private static void AddStressRecords(
        ExperimentManifest manifest,
        RunProvenance provenance,
        string ablationProfile,
        double boundaryScale,
        int replicate,
        int seed,
        List<ExperimentResultRecord> records)
    {
        foreach (SyntheticStressTrace stressTrace in SyntheticStressTraceCatalog.All.OrderBy(static item => item.Kind))
        {
            ReplayTrace trace = ExperimentTraceTransformer.Transform(
                stressTrace.Trace,
                boundaryScale,
                CombineSeed(seed, (int)stressTrace.Kind, sourceSalt: 31),
                manifest.MaximumSignalNoiseDb,
                manifest.MaximumTimingJitterSeconds);
            SyntheticStressTrace transformed = stressTrace with { Trace = trace };

            long started = manifest.CaptureTiming ? Stopwatch.GetTimestamp() : 0L;
            RpspDetectionPipeline pipeline = ExperimentAblationProfiles.CreatePipeline(ablationProfile);
            ReplayPipelineRunResult result = ReplayPipelineRunner.Run(transformed, pipeline);
            double elapsed = GetElapsedMilliseconds(manifest.CaptureTiming, started);
            string runId = CreateRunId(manifest.ExperimentId, "stress", stressTrace.Kind.ToString(), ablationProfile, boundaryScale, replicate, seed);

            AddWindowRecords(
                manifest,
                provenance,
                "stress",
                stressTrace.Kind.ToString(),
                ablationProfile,
                boundaryScale,
                replicate,
                seed,
                runId,
                result,
                elapsed,
                (detector, window) => GetStressLabel(stressTrace, detector, window),
                records);
        }
    }

    private static void AddWindowRecords(
        ExperimentManifest manifest,
        RunProvenance provenance,
        string sourceKind,
        string sourceName,
        string ablationProfile,
        double boundaryScale,
        int replicate,
        int seed,
        string runId,
        ReplayPipelineRunResult result,
        double elapsedMilliseconds,
        Func<string, string, string> labelSelector,
        List<ExperimentResultRecord> records)
    {
        foreach (ReplayPipelineWindowResult window in result.Windows)
        {
            foreach (DetectorRiskContribution contribution in window.PipelineResult.DetectorContributions)
            {
                records.Add(CreateRecord(
                    manifest,
                    provenance,
                    sourceKind,
                    sourceName,
                    window,
                    ablationProfile,
                    boundaryScale,
                    replicate,
                    seed,
                    runId,
                    labelSelector(contribution.DetectorName, window.WindowName),
                    contribution.DetectorName,
                    contribution.RiskScore.Value,
                    contribution.WarningLevel.ToString(),
                    contribution.IsSuppressed,
                    contribution.SuppressionReason,
                    contribution.Evidence.Items,
                    elapsedMilliseconds));
            }

            CompositeRiskAssessment composite = window.CompositeAssessment;
            records.Add(CreateRecord(
                manifest,
                provenance,
                sourceKind,
                sourceName,
                window,
                ablationProfile,
                boundaryScale,
                replicate,
                seed,
                runId,
                labelSelector("CompositeRiskScorer", window.WindowName),
                "CompositeRiskScorer",
                composite.RiskScore.Value,
                composite.WarningDecision.Level.ToString(),
                composite.IsSuppressed,
                composite.IsSuppressed ? "all-detectors-suppressed" : "none",
                composite.Evidence.Items,
                elapsedMilliseconds));
        }
    }

    private static ExperimentResultRecord CreateRecord(
        ExperimentManifest manifest,
        RunProvenance provenance,
        string sourceKind,
        string sourceName,
        ReplayPipelineWindowResult window,
        string ablationProfile,
        double boundaryScale,
        int replicate,
        int seed,
        string runId,
        string label,
        string detector,
        double score,
        string warning,
        bool suppressed,
        string? suppressionReason,
        IReadOnlyList<EvidenceItem> evidence,
        double elapsedMilliseconds)
    {
        return new ExperimentResultRecord(
            manifest.SchemaVersion,
            manifest.ExperimentId,
            runId,
            provenance.GitCommit,
            provenance.DotnetSdkVersion,
            provenance.RuntimeVersion,
            provenance.ConfigurationHash,
            sourceKind,
            sourceName,
            window.WindowName,
            ablationProfile,
            boundaryScale,
            replicate,
            seed,
            label,
            detector,
            score,
            GetBand(score),
            warning,
            suppressed ? "suppressed" : "active",
            suppressionReason ?? "none",
            GetEvidenceLevel(evidence),
            DataSource,
            window.ObservationCount,
            elapsedMilliseconds,
            provenance.TimingMode);
    }

    private static string GetReplayLabel(ReplayScenario scenario, string detector)
    {
        return detector switch
        {
            "StationaryHandoverAnomalyDetector" when scenario.HasStationaryHandoverExpectation =>
                GetLabel(scenario.ExpectedStationaryHandoverSuppression, scenario.MinimumStationaryHandoverScore, scenario.MaximumStationaryHandoverScore),
            "RatDowngradeRiskDetector" when scenario.HasRatDowngradeExpectation =>
                GetLabel(scenario.ExpectedRatDowngradeSuppression, scenario.MinimumRatDowngradeScore, scenario.MaximumRatDowngradeScore),
            "UnknownStrongCellDetector" when scenario.HasUnknownStrongCellExpectation =>
                GetLabel(scenario.ExpectedUnknownStrongCellSuppression, scenario.MinimumUnknownStrongCellScore, scenario.MaximumUnknownStrongCellScore),
            "NeighborGraphAnomalyDetector" when scenario.HasNeighborGraphExpectation =>
                GetLabel(scenario.ExpectedNeighborGraphSuppression, scenario.MinimumNeighborGraphScore, scenario.MaximumNeighborGraphScore),
            _ => "unspecified"
        };
    }

    private static string GetStressLabel(SyntheticStressTrace trace, string detector, string window)
    {
        if (trace.Expectation.FocusWindowName is string focus
            && !string.Equals(focus, window, StringComparison.Ordinal))
        {
            return trace.Expectation.ExpectsEarlyColdStartSuppression ? "suppression-expected" : "unspecified";
        }

        return detector switch
        {
            "StationaryHandoverAnomalyDetector" => GetLabel(false, trace.Expectation.MinimumStationaryHandoverScore, trace.Expectation.MaximumStationaryHandoverScore),
            "RatDowngradeRiskDetector" => GetLabel(false, trace.Expectation.MinimumRatDowngradeScore, trace.Expectation.MaximumRatDowngradeScore),
            "UnknownStrongCellDetector" => GetLabel(false, trace.Expectation.MinimumUnknownStrongCellScore, trace.Expectation.MaximumUnknownStrongCellScore),
            "NeighborGraphAnomalyDetector" => GetLabel(false, trace.Expectation.MinimumNeighborGraphScore, trace.Expectation.MaximumNeighborGraphScore),
            "CompositeRiskScorer" => GetLabel(false, trace.Expectation.MinimumCompositeScore, trace.Expectation.MaximumCompositeScore),
            _ => "unspecified"
        };
    }

    private static string GetLabel(bool suppressionExpected, double? minimum, double? maximum)
    {
        if (suppressionExpected)
        {
            return "suppression-expected";
        }

        if (minimum >= 0.5D)
        {
            return "positive";
        }

        if (maximum < 0.5D)
        {
            return "negative";
        }

        return "unspecified";
    }

    private static string CreateRunId(
        string experimentId,
        string sourceKind,
        string sourceName,
        string ablationProfile,
        double boundaryScale,
        int replicate,
        int seed)
    {
        return FormattableString.Invariant(
            $"{experimentId}:{sourceKind}:{sourceName}:{ablationProfile}:scale-{boundaryScale:F2}:rep-{replicate:D2}:seed-{seed}");
    }

    private static int CombineSeed(int seed, int sourceValue, int sourceSalt)
    {
        return unchecked((seed * 397) ^ (sourceValue * 31) ^ sourceSalt);
    }

    private static double GetElapsedMilliseconds(bool captureTiming, long started)
    {
        return captureTiming ? Stopwatch.GetElapsedTime(started).TotalMilliseconds : 0D;
    }

    private static string GetBand(double score)
    {
        return score >= 0.8D ? "high" : score >= 0.5D ? "elevated" : "low";
    }

    private static string GetEvidenceLevel(IReadOnlyList<EvidenceItem> items)
    {
        if (items.Count == 0)
        {
            return "none";
        }

        double maximum = items.Max(static item => item.Weight);
        return maximum >= 0.8D ? "high" : maximum >= 0.4D ? "medium" : "low";
    }
}
