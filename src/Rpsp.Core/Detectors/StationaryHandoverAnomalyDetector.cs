using System.Globalization;
using System.Runtime.CompilerServices;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Detectors;

public sealed class StationaryHandoverAnomalyDetector
{
    private const double SuspiciousScore = 0.6D;
    private const double HighRiskScore = 0.8D;
    private readonly StationaryHandoverDetectorOptions _options;

    public StationaryHandoverAnomalyDetector(StationaryHandoverDetectorOptions? options = null)
    {
        _options = options ?? new StationaryHandoverDetectorOptions();
        ValidateOptions(_options);
    }

    public StationaryHandoverDetectionResult Analyze(IEnumerable<RadioObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        RadioObservation[] orderedObservations = observations
            .OrderBy(static observation => observation.ObservedAt)
            .ToArray();

        return AnalyzeOrdered(orderedObservations);
    }

    public async ValueTask<StationaryHandoverDetectionResult> AnalyzeAsync(
        IAsyncEnumerable<RadioObservation> observations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var collected = new List<RadioObservation>();

        await foreach (RadioObservation observation in observations
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            collected.Add(observation);
        }

        return Analyze(collected);
    }

    private static void ValidateOptions(StationaryHandoverDetectorOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumObservationCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.MinimumWindowDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.SuspiciousTransitionCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.HighRiskTransitionCount, options.SuspiciousTransitionCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.SuspiciousDistinctServingCells, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            options.HighRiskDistinctServingCells,
            options.SuspiciousDistinctServingCells);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.SuspiciousTransitionsPerMinute);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            options.HighRiskTransitionsPerMinute,
            options.SuspiciousTransitionsPerMinute);
    }

    private StationaryHandoverDetectionResult AnalyzeOrdered(IReadOnlyList<RadioObservation> observations)
    {
        TimeSpan windowDuration = CalculateWindowDuration(observations);
        ServingCellObservation[] servingCells = observations
            .Select(TryCreateServingCellObservation)
            .Where(static observation => observation is not null)
            .Cast<ServingCellObservation>()
            .ToArray();
        MotionSummary motionSummary = SummarizeMotion(observations);
        HandoverMetrics metrics = CalculateMetrics(servingCells, windowDuration);
        string? suppressionReason = GetSuppressionReason(
            observations,
            windowDuration,
            servingCells,
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

        return new StationaryHandoverDetectionResult(
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
        IReadOnlyCollection<ServingCellObservation> servingCells,
        MotionSummary motionSummary,
        HandoverMetrics metrics)
    {
        if (observations.Count < _options.MinimumObservationCount)
        {
            return "suppressed:cold-start-observation-count";
        }

        if (windowDuration < _options.MinimumWindowDuration)
        {
            return "suppressed:cold-start-window-duration";
        }

        if (servingCells.Count == 0)
        {
            return "suppressed:no-serving-cell-snapshots";
        }

        if (motionSummary.IsBenignMobilityDominant)
        {
            return "suppressed:benign-motion-state";
        }

        if (!motionSummary.IsStationaryLike)
        {
            return "suppressed:insufficient-stationary-motion-evidence";
        }

        if (metrics.TransitionCount == 0)
        {
            return "suppressed:no-serving-cell-churn";
        }

        if (metrics.DistinctServingCellCount < 2)
        {
            return "suppressed:single-serving-cell";
        }

        return null;
    }

    private double CalculateScore(HandoverMetrics metrics)
    {
        double transitionScore = ScoreThreshold(
            metrics.TransitionCount,
            _options.SuspiciousTransitionCount,
            _options.HighRiskTransitionCount);
        double distinctCellScore = ScoreThreshold(
            metrics.DistinctServingCellCount,
            _options.SuspiciousDistinctServingCells,
            _options.HighRiskDistinctServingCells);
        double rateScore = ScoreThreshold(
            metrics.TransitionsPerMinute,
            _options.SuspiciousTransitionsPerMinute,
            _options.HighRiskTransitionsPerMinute);
        double score = Math.Max(transitionScore, Math.Max(distinctCellScore, rateScore));

        if (metrics.HasRepeatedChurnPattern && score >= SuspiciousScore)
        {
            score += 0.08D;
        }

        return Math.Clamp(score, 0D, 1D);
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

    private static HandoverMetrics CalculateMetrics(
        IReadOnlyList<ServingCellObservation> servingCells,
        TimeSpan windowDuration)
    {
        string[] servingCellKeys = servingCells
            .Select(static servingCell => servingCell.CellKey)
            .ToArray();
        string[] churnSequence = servingCellKeys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
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
        int transitionCount = Math.Max(0, churnSequence.Length - 1);
        int distinctServingCellCount = churnSequence.Distinct(StringComparer.Ordinal).Count();
        double transitionsPerMinute = windowDuration.TotalMinutes <= 0D
            ? 0D
            : transitionCount / windowDuration.TotalMinutes;
        bool hasRepeatedChurnPattern = HasRepeatedChurnPattern(churnSequence);

        return new HandoverMetrics(
            distinctServingCellCount,
            transitionCount,
            transitionsPerMinute,
            hasRepeatedChurnPattern);
    }

    private static bool HasRepeatedChurnPattern(IEnumerable<string> churnSequence)
    {
        var seenCells = new HashSet<string>(StringComparer.Ordinal);

        foreach (string cellKey in churnSequence)
        {
            if (!seenCells.Add(cellKey))
            {
                return true;
            }
        }

        return false;
    }

    private static ServingCellObservation? TryCreateServingCellObservation(RadioObservation observation)
    {
        CellSnapshot? servingCell = observation.CellSnapshots.FirstOrDefault(
            static cell => cell.Role == CellRole.Serving || cell.IsRegistered == true);

        return servingCell is null
            ? null
            : new ServingCellObservation(observation.ObservedAt, CreateCellKey(servingCell));
    }

    private static string CreateCellKey(CellSnapshot cell)
    {
        CellIdentifier? identifier = cell.Identifier;
        string[] parts =
        [
            cell.RadioAccessTechnology.ToString(),
            identifier?.Plmn?.MobileCountryCode ?? string.Empty,
            identifier?.Plmn?.MobileNetworkCode ?? string.Empty,
            identifier?.NetworkCellId ?? string.Empty,
            ToInvariantString(identifier?.PhysicalCellId),
            ToInvariantString(identifier?.TrackingAreaCode),
            ToInvariantString(identifier?.ChannelNumber)
        ];

        return string.Join('\u001F', parts);
    }

    private static string ToInvariantString(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static EvidenceBundle CreateEvidence(
        IReadOnlyList<RadioObservation> observations,
        TimeSpan windowDuration,
        MotionSummary motionSummary,
        HandoverMetrics metrics,
        string? suppressionReason)
    {
        string windowSeconds = windowDuration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);
        string transitionRate = metrics.TransitionsPerMinute.ToString("F2", CultureInfo.InvariantCulture);
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
                "DistinctServingCellCount",
                $"Distinct serving cell count: {metrics.DistinctServingCellCount}.",
                metrics.DistinctServingCellCount),
            CreateEvidenceItem(
                "ServingCellTransitionCount",
                $"Serving cell transition count: {metrics.TransitionCount}.",
                metrics.TransitionCount),
            CreateEvidenceItem(
                "TransitionsPerMinute",
                $"Serving cell transitions per minute: {transitionRate}.",
                metrics.TransitionsPerMinute),
            CreateEvidenceItem(
                "RepeatedChurnPattern",
                $"Repeated churn pattern detected: {metrics.HasRepeatedChurnPattern}.",
                metrics.HasRepeatedChurnPattern ? 1D : 0D),
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
        HandoverMetrics metrics)
    {
        DateTimeOffset observedAt = observations.Count == 0
            ? DateTimeOffset.UnixEpoch
            : observations[^1].ObservedAt;

        return new AnomalyEvent(
            CreateEventId(evidence.BundleId),
            AnomalyType.StationaryHandover,
            evidence,
            observedAt,
            $"Stationary serving-cell churn: transitions={metrics.TransitionCount}, distinctCells={metrics.DistinctServingCellCount}.");
    }

    private static string CreateBundleId(IReadOnlyList<RadioObservation> observations, HandoverMetrics metrics)
    {
        long firstTicks = observations.Count == 0 ? 0L : observations[0].ObservedAt.UtcTicks;
        long lastTicks = observations.Count == 0 ? 0L : observations[^1].ObservedAt.UtcTicks;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"stationary-handover-evidence:{firstTicks}:{lastTicks}:{metrics.TransitionCount}:{metrics.DistinctServingCellCount}");
    }

    private static string CreateEventId(string bundleId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"event:{bundleId}");
    }

    private static string CreateDecisionId(string bundleId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"decision:{bundleId}");
    }

    private sealed record ServingCellObservation(DateTimeOffset ObservedAt, string CellKey);

    private sealed record HandoverMetrics(
        int DistinctServingCellCount,
        int TransitionCount,
        double TransitionsPerMinute,
        bool HasRepeatedChurnPattern);

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
