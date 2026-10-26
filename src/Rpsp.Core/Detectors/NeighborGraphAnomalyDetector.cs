using System.Globalization;
using Rpsp.Core.Baselines;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Detectors;

public sealed class NeighborGraphAnomalyDetector
{
    private const double SuspiciousScore = 0.6D;
    private const double HighRiskScore = 0.8D;
    private readonly NeighborGraphDetectorOptions _options;

    public NeighborGraphAnomalyDetector(NeighborGraphDetectorOptions? options = null)
    {
        _options = options ?? new NeighborGraphDetectorOptions();
        ValidateOptions(_options);
    }

    public NeighborGraphDetectionResult Analyze(
        IEnumerable<RadioObservation> observations,
        BaselineProfile baseline)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(baseline);

        return Analyze(observations, baseline.TrustedCells);
    }

    public NeighborGraphDetectionResult Analyze(
        IEnumerable<RadioObservation> observations,
        IEnumerable<TrustedCellSignature> baselineNeighborCells)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(baselineNeighborCells);

        RadioObservation[] orderedObservations = observations
            .OrderBy(static observation => observation.ObservedAt)
            .ToArray();
        TrustedCellSignature[] baselineSignatures = baselineNeighborCells.ToArray();

        return AnalyzeOrdered(orderedObservations, baselineSignatures);
    }

    public async ValueTask<NeighborGraphDetectionResult> AnalyzeAsync(
        IAsyncEnumerable<RadioObservation> observations,
        BaselineProfile baseline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(baseline);

        var collected = new List<RadioObservation>();

        await foreach (RadioObservation observation in observations
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            collected.Add(observation);
        }

        return Analyze(collected, baseline);
    }

    private static void ValidateOptions(NeighborGraphDetectorOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumObservationCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.MinimumWindowDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumBaselineNeighborCellCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumObservedNeighborCellCount, 1);
        ValidateUnitInterval(options.SuspiciousJaccardSimilarityThreshold, nameof(options.SuspiciousJaccardSimilarityThreshold));
        ValidateUnitInterval(options.HighRiskJaccardSimilarityThreshold, nameof(options.HighRiskJaccardSimilarityThreshold));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            options.HighRiskJaccardSimilarityThreshold,
            options.SuspiciousJaccardSimilarityThreshold);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumLowSimilarityObservations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            options.HighRiskLowSimilarityObservations,
            options.MinimumLowSimilarityObservations);
        ValidateUnitInterval(options.ServingCellChangeBoost, nameof(options.ServingCellChangeBoost));
        ValidateUnitInterval(options.RepeatedDriftBoost, nameof(options.RepeatedDriftBoost));
        ValidateUnitInterval(options.MaximumCorrelationBoost, nameof(options.MaximumCorrelationBoost));
    }

    private NeighborGraphDetectionResult AnalyzeOrdered(
        IReadOnlyList<RadioObservation> observations,
        TrustedCellSignature[] baselineNeighborCells)
    {
        TimeSpan windowDuration = CalculateWindowDuration(observations);
        MotionSummary motionSummary = SummarizeMotion(observations);
        string[] baselineNeighborKeys = CreateBaselineNeighborKeys(baselineNeighborCells);
        NeighborGraphObservation[] graphObservations = CreateGraphObservations(observations, baselineNeighborKeys);
        NeighborGraphMetrics metrics = CalculateMetrics(
            graphObservations,
            windowDuration,
            baselineNeighborKeys.Length);
        string? suppressionReason = GetSuppressionReason(
            observations,
            windowDuration,
            motionSummary,
            metrics);
        bool isSuppressed = suppressionReason is not null;
        double scoreValue = isSuppressed ? 0D : CalculateScore(metrics);
        RiskSeverity severity = ToSeverity(scoreValue);
        WarningLevel warningLevel = ToWarningLevel(severity);
        EvidenceBundle evidence = CreateEvidence(
            observations,
            windowDuration,
            motionSummary,
            metrics,
            suppressionReason);
        var riskScore = new RiskScore(scoreValue, severity, confidence: isSuppressed ? 1D : 0.8D);
        AnomalyEvent? anomalyEvent = scoreValue >= SuspiciousScore
            ? CreateAnomalyEvent(evidence, observations, metrics)
            : null;
        var warningDecision = new WarningDecision(
            CreateDecisionId(evidence.BundleId),
            warningLevel,
            riskScore,
            anomalyEvent);

        return new NeighborGraphDetectionResult(
            evidence,
            riskScore,
            warningDecision,
            anomalyEvent,
            isSuppressed,
            suppressionReason);
    }

    private string? GetSuppressionReason(
        IReadOnlyCollection<RadioObservation> observations,
        TimeSpan windowDuration,
        MotionSummary motionSummary,
        NeighborGraphMetrics metrics)
    {
        if (observations.Count < _options.MinimumObservationCount)
        {
            return "suppressed:cold-start-observation-count";
        }

        if (windowDuration < _options.MinimumWindowDuration)
        {
            return "suppressed:cold-start-window-duration";
        }

        if (metrics.BaselineNeighborCellCount < _options.MinimumBaselineNeighborCellCount)
        {
            return "suppressed:insufficient-neighbor-baseline";
        }

        if (metrics.EligibleNeighborObservationCount == 0)
        {
            return "suppressed:missing-neighbor-cells";
        }

        if (metrics.ObservedNeighborCellCount < _options.MinimumObservedNeighborCellCount)
        {
            return "suppressed:insufficient-observed-neighbor-graph";
        }

        if (motionSummary.IsBenignMobilityDominant)
        {
            return "suppressed:benign-motion-state";
        }

        if (!motionSummary.IsStationaryLike)
        {
            return "suppressed:insufficient-stationary-motion-evidence";
        }

        if (metrics.LowSimilarityObservationCount == 0)
        {
            return "suppressed:no-neighbor-graph-drift";
        }

        if (metrics.LowSimilarityObservationCount < _options.MinimumLowSimilarityObservations)
        {
            return "suppressed:insufficient-low-similarity-observations";
        }

        return null;
    }

    private double CalculateScore(NeighborGraphMetrics metrics)
    {
        double similarityScore = ScoreSimilarity(metrics.MinimumJaccardSimilarity);
        double repeatedScore = ScoreThreshold(
            metrics.LowSimilarityObservationCount,
            _options.MinimumLowSimilarityObservations,
            _options.HighRiskLowSimilarityObservations);
        double score = Math.Max(similarityScore, repeatedScore);
        double boost = 0D;

        if (score >= SuspiciousScore && metrics.ServingCellTransitionCount > 0)
        {
            boost += _options.ServingCellChangeBoost;
        }

        if (score >= SuspiciousScore
            && metrics.LowSimilarityObservationCount > _options.MinimumLowSimilarityObservations)
        {
            boost += _options.RepeatedDriftBoost;
        }

        return Math.Clamp(score + Math.Min(boost, _options.MaximumCorrelationBoost), 0D, 1D);
    }

    private double ScoreSimilarity(double minimumSimilarity)
    {
        if (minimumSimilarity > _options.SuspiciousJaccardSimilarityThreshold)
        {
            return 0D;
        }

        if (minimumSimilarity <= _options.HighRiskJaccardSimilarityThreshold)
        {
            return HighRiskScore;
        }

        double thresholdRange = _options.SuspiciousJaccardSimilarityThreshold
            - _options.HighRiskJaccardSimilarityThreshold;
        double normalized = thresholdRange <= 0D
            ? 1D
            : (_options.SuspiciousJaccardSimilarityThreshold - minimumSimilarity) / thresholdRange;

        return SuspiciousScore + ((HighRiskScore - SuspiciousScore) * normalized);
    }

    private static double ScoreThreshold(double value, double suspiciousThreshold, double highThreshold)
    {
        if (value < suspiciousThreshold)
        {
            return 0D;
        }

        if (value >= highThreshold)
        {
            return HighRiskScore;
        }

        double thresholdRange = highThreshold - suspiciousThreshold;
        double normalized = thresholdRange <= 0D
            ? 1D
            : (value - suspiciousThreshold) / thresholdRange;

        return SuspiciousScore + ((HighRiskScore - SuspiciousScore) * normalized);
    }

    private static RiskSeverity ToSeverity(double score)
    {
        if (score >= HighRiskScore)
        {
            return RiskSeverity.High;
        }

        if (score >= SuspiciousScore)
        {
            return RiskSeverity.Medium;
        }

        if (score > 0D)
        {
            return RiskSeverity.Low;
        }

        return RiskSeverity.None;
    }

    private static WarningLevel ToWarningLevel(RiskSeverity severity)
    {
        return severity switch
        {
            RiskSeverity.High => WarningLevel.Warning,
            RiskSeverity.Critical => WarningLevel.Critical,
            RiskSeverity.Medium => WarningLevel.Caution,
            RiskSeverity.Low => WarningLevel.Informational,
            _ => WarningLevel.None
        };
    }

    private static TimeSpan CalculateWindowDuration(IReadOnlyList<RadioObservation> observations)
    {
        if (observations.Count < 2)
        {
            return TimeSpan.Zero;
        }

        return observations[^1].ObservedAt - observations[0].ObservedAt;
    }

    private MotionSummary SummarizeMotion(IEnumerable<RadioObservation> observations)
    {
        DeviceMotionState[] motionStates = observations
            .Select(static observation => observation.DeviceMotion?.State ?? DeviceMotionState.Unknown)
            .ToArray();
        int stationaryLikeCount = motionStates.Count(_options.StationaryMotionStates.Contains);
        int benignMobilityCount = motionStates.Count(_options.BenignMotionStates.Contains);
        int unknownCount = motionStates.Count(static state => state == DeviceMotionState.Unknown);

        return new MotionSummary(
            stationaryLikeCount,
            benignMobilityCount,
            unknownCount,
            motionStates.Length);
    }

    private static string[] CreateBaselineNeighborKeys(IEnumerable<TrustedCellSignature> baselineNeighborCells)
    {
        return baselineNeighborCells
            .Select(CreateSignatureKey)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static NeighborGraphObservation[] CreateGraphObservations(
        IReadOnlyList<RadioObservation> observations,
        IReadOnlyCollection<string> baselineNeighborKeys)
    {
        var graphObservations = new List<NeighborGraphObservation>();

        foreach (RadioObservation observation in observations)
        {
            string? servingCellKey = observation.CellSnapshots
                .Where(static cell => cell.Role == CellRole.Serving || cell.IsRegistered == true)
                .Select(CreateCellKey)
                .FirstOrDefault(static key => !string.IsNullOrWhiteSpace(key));
            string[] neighborKeys = observation.CellSnapshots
                .Where(static cell => cell.Role == CellRole.Neighbor && cell.IsRegistered != true)
                .Select(CreateCellKey)
                .Where(static key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            double? similarity = neighborKeys.Length == 0
                ? null
                : CalculateJaccardSimilarity(neighborKeys, baselineNeighborKeys);

            graphObservations.Add(new NeighborGraphObservation(
                observation.ObservedAt,
                servingCellKey,
                neighborKeys,
                similarity));
        }

        return graphObservations.ToArray();
    }

    private NeighborGraphMetrics CalculateMetrics(
        IReadOnlyList<NeighborGraphObservation> graphObservations,
        TimeSpan windowDuration,
        int baselineNeighborCellCount)
    {
        NeighborGraphObservation[] eligibleObservations = graphObservations
            .Where(observation => observation.NeighborKeys.Count >= _options.MinimumObservedNeighborCellCount)
            .ToArray();
        double[] similarities = eligibleObservations
            .Where(static observation => observation.JaccardSimilarity is not null)
            .Select(static observation => observation.JaccardSimilarity!.Value)
            .ToArray();
        int lowSimilarityCount = similarities.Count(
            similarity => similarity < _options.SuspiciousJaccardSimilarityThreshold);
        int severeDriftCount = similarities.Count(
            similarity => similarity <= _options.HighRiskJaccardSimilarityThreshold);
        string[] allObservedNeighborKeys = graphObservations
            .SelectMany(static observation => observation.NeighborKeys)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        int servingTransitions = CalculateServingTransitions(graphObservations);
        double averageSimilarity = similarities.Length == 0 ? 1D : similarities.Average();
        double minimumSimilarity = similarities.Length == 0 ? 1D : similarities.Min();

        return new NeighborGraphMetrics(
            baselineNeighborCellCount,
            allObservedNeighborKeys.Length,
            eligibleObservations.Length,
            averageSimilarity,
            minimumSimilarity,
            lowSimilarityCount,
            severeDriftCount,
            servingTransitions,
            lowSimilarityCount >= _options.MinimumLowSimilarityObservations,
            _options.SuspiciousJaccardSimilarityThreshold,
            _options.HighRiskJaccardSimilarityThreshold,
            windowDuration.TotalMinutes <= 0D ? 0D : lowSimilarityCount / windowDuration.TotalMinutes);
    }

    private static int CalculateServingTransitions(IReadOnlyList<NeighborGraphObservation> graphObservations)
    {
        string[] servingSequence = graphObservations
            .Select(static observation => observation.ServingCellKey)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Cast<string>()
            .Aggregate(
                new List<string>(),
                static (compressed, key) =>
                {
                    if (compressed.Count == 0 || !string.Equals(compressed[^1], key, StringComparison.Ordinal))
                    {
                        compressed.Add(key);
                    }

                    return compressed;
                })
            .ToArray();

        return Math.Max(0, servingSequence.Length - 1);
    }

    private static double CalculateJaccardSimilarity(
        IEnumerable<string> observedNeighborKeys,
        IEnumerable<string> baselineNeighborKeys)
    {
        var observed = observedNeighborKeys.ToHashSet(StringComparer.Ordinal);
        var baseline = baselineNeighborKeys.ToHashSet(StringComparer.Ordinal);

        if (observed.Count == 0 || baseline.Count == 0)
        {
            return 0D;
        }

        int intersection = observed.Count(key => baseline.Contains(key));
        int union = observed.Union(baseline, StringComparer.Ordinal).Count();

        return union == 0 ? 0D : (double)intersection / union;
    }

    private static string CreateCellKey(CellSnapshot cell)
    {
        CellIdentifier? identifier = cell.Identifier;

        if (!string.IsNullOrWhiteSpace(identifier?.NetworkCellId))
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{cell.RadioAccessTechnology}:{identifier.NetworkCellId}");
        }

        if (identifier?.PhysicalCellId is { } physicalCellId && identifier.ChannelNumber is { } channelNumber)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{cell.RadioAccessTechnology}:pci:{physicalCellId}:chan:{channelNumber}");
        }

        return string.Empty;
    }

    private static string CreateSignatureKey(TrustedCellSignature signature)
    {
        if (!string.IsNullOrWhiteSpace(signature.NetworkCellPseudonym))
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{signature.RadioAccessTechnology}:{signature.NetworkCellPseudonym}");
        }

        if (signature.PhysicalCellId is { } physicalCellId && signature.ChannelNumber is { } channelNumber)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{signature.RadioAccessTechnology}:pci:{physicalCellId}:chan:{channelNumber}");
        }

        return string.Empty;
    }

    private static EvidenceBundle CreateEvidence(
        IReadOnlyList<RadioObservation> observations,
        TimeSpan windowDuration,
        MotionSummary motionSummary,
        NeighborGraphMetrics metrics,
        string? suppressionReason)
    {
        string windowSeconds = windowDuration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);
        string averageSimilarity = metrics.AverageJaccardSimilarity.ToString("F3", CultureInfo.InvariantCulture);
        string minimumSimilarity = metrics.MinimumJaccardSimilarity.ToString("F3", CultureInfo.InvariantCulture);
        string suspiciousThreshold = metrics.SuspiciousSimilarityThreshold.ToString("F3", CultureInfo.InvariantCulture);
        string highRiskThreshold = metrics.HighRiskSimilarityThreshold.ToString("F3", CultureInfo.InvariantCulture);
        EvidenceItem[] items =
        [
            CreateEvidenceItem(
                "ObservationCount",
                $"Observation count: {observations.Count}.",
                observations.Count),
            CreateEvidenceItem(
                "WindowDuration",
                $"Window duration seconds: {windowSeconds}.",
                windowDuration.TotalSeconds),
            CreateEvidenceItem(
                "MotionStateSummary",
                $"Stationary-like: {motionSummary.StationaryLikeCount}; benign mobility: {motionSummary.BenignMobilityCount}; unknown: {motionSummary.UnknownCount}.",
                motionSummary.StationaryLikeCount),
            CreateEvidenceItem(
                "BaselineNeighborCellCount",
                $"Baseline neighbor cell count: {metrics.BaselineNeighborCellCount}.",
                metrics.BaselineNeighborCellCount),
            CreateEvidenceItem(
                "ObservedNeighborCellCount",
                $"Observed neighbor cell count: {metrics.ObservedNeighborCellCount}.",
                metrics.ObservedNeighborCellCount),
            CreateEvidenceItem(
                "AverageJaccardSimilarity",
                $"Average Jaccard similarity: {averageSimilarity}.",
                1D - metrics.AverageJaccardSimilarity),
            CreateEvidenceItem(
                "MinimumJaccardSimilarity",
                $"Minimum Jaccard similarity: {minimumSimilarity}.",
                1D - metrics.MinimumJaccardSimilarity),
            CreateEvidenceItem(
                "LowSimilarityObservationCount",
                $"Low similarity observation count: {metrics.LowSimilarityObservationCount}.",
                metrics.LowSimilarityObservationCount),
            CreateEvidenceItem(
                "SevereDriftObservationCount",
                $"Severe drift observation count: {metrics.SevereDriftObservationCount}.",
                metrics.SevereDriftObservationCount),
            CreateEvidenceItem(
                "ServingCellTransitionCount",
                $"Serving cell transition count: {metrics.ServingCellTransitionCount}.",
                metrics.ServingCellTransitionCount),
            CreateEvidenceItem(
                "RepeatedDriftDetected",
                $"Repeated neighbor graph drift detected: {metrics.HasRepeatedDrift}.",
                metrics.HasRepeatedDrift ? 1D : 0D),
            CreateEvidenceItem(
                "SimilarityThresholdsUsed",
                $"Similarity thresholds used: suspicious={suspiciousThreshold}; highRisk={highRiskThreshold}.",
                1D - metrics.HighRiskSimilarityThreshold),
            CreateEvidenceItem(
                "SuppressionReason",
                $"Suppression reason: {suppressionReason ?? "none"}.",
                suppressionReason is null ? 0D : 1D)
        ];

        return new EvidenceBundle(
            CreateBundleId(observations, metrics),
            items,
            observations);
    }

    private static EvidenceItem CreateEvidenceItem(string name, string description, double weight)
    {
        return new EvidenceItem(name, description, ObservationAvailability.Derived, Math.Clamp(weight, 0D, 1D));
    }

    private static AnomalyEvent CreateAnomalyEvent(
        EvidenceBundle evidence,
        IReadOnlyList<RadioObservation> observations,
        NeighborGraphMetrics metrics)
    {
        DateTimeOffset observedAt = observations.Count == 0
            ? DateTimeOffset.UnixEpoch
            : observations[^1].ObservedAt;
        string minimumSimilarity = metrics.MinimumJaccardSimilarity.ToString("F3", CultureInfo.InvariantCulture);

        return new AnomalyEvent(
            CreateEventId(evidence.BundleId),
            AnomalyType.NeighborGraphAnomaly,
            evidence,
            observedAt,
            $"Neighbor graph anomaly: minimumSimilarity={minimumSimilarity}, lowSimilarityObservations={metrics.LowSimilarityObservationCount}.");
    }

    private static string CreateBundleId(IReadOnlyList<RadioObservation> observations, NeighborGraphMetrics metrics)
    {
        long firstTicks = observations.Count == 0 ? 0L : observations[0].ObservedAt.UtcTicks;
        long lastTicks = observations.Count == 0 ? 0L : observations[^1].ObservedAt.UtcTicks;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"neighbor-graph-evidence:{firstTicks}:{lastTicks}:{metrics.BaselineNeighborCellCount}:{metrics.ObservedNeighborCellCount}:{metrics.LowSimilarityObservationCount}:{metrics.SevereDriftObservationCount}");
    }

    private static string CreateEventId(string bundleId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"event:{bundleId}");
    }

    private static string CreateDecisionId(string bundleId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"decision:{bundleId}");
    }

    private static void ValidateUnitInterval(double value, string parameterName)
    {
        if (double.IsNaN(value) || value < 0D || value > 1D)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private sealed record NeighborGraphObservation(
        DateTimeOffset ObservedAt,
        string? ServingCellKey,
        IReadOnlyList<string> NeighborKeys,
        double? JaccardSimilarity);

    private sealed record NeighborGraphMetrics(
        int BaselineNeighborCellCount,
        int ObservedNeighborCellCount,
        int EligibleNeighborObservationCount,
        double AverageJaccardSimilarity,
        double MinimumJaccardSimilarity,
        int LowSimilarityObservationCount,
        int SevereDriftObservationCount,
        int ServingCellTransitionCount,
        bool HasRepeatedDrift,
        double SuspiciousSimilarityThreshold,
        double HighRiskSimilarityThreshold,
        double LowSimilarityObservationsPerMinute);

    private sealed record MotionSummary(
        int StationaryLikeCount,
        int BenignMobilityCount,
        int UnknownCount,
        int TotalCount)
    {
        public bool IsStationaryLike => StationaryLikeCount > 0 && StationaryLikeCount >= BenignMobilityCount;

        public bool IsBenignMobilityDominant => BenignMobilityCount > StationaryLikeCount;
    }
}
