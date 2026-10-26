using System.Globalization;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Detectors;

public sealed class RatDowngradeRiskDetector
{
    private const double SuspiciousScore = 0.6D;
    private const double HighRiskScore = 0.8D;
    private readonly RatDowngradeDetectorOptions _options;

    public RatDowngradeRiskDetector(RatDowngradeDetectorOptions? options = null)
    {
        _options = options ?? new RatDowngradeDetectorOptions();
        ValidateOptions(_options);
    }

    public RatDowngradeDetectionResult Analyze(IEnumerable<RadioObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        RadioObservation[] orderedObservations = observations
            .OrderBy(static observation => observation.ObservedAt)
            .ToArray();

        return AnalyzeOrdered(orderedObservations);
    }

    public async ValueTask<RatDowngradeDetectionResult> AnalyzeAsync(
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

    private static void ValidateOptions(RatDowngradeDetectorOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumObservationCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.MinimumWindowDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumModernRatHistoryCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.SuspiciousDowngradeCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.HighRiskDowngradeCount, options.SuspiciousDowngradeCount);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.SuspiciousLegacyDwellDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            options.HighRiskLegacyDwellDuration,
            options.SuspiciousLegacyDwellDuration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.SuspiciousDowngradesPerMinute);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            options.HighRiskDowngradesPerMinute,
            options.SuspiciousDowngradesPerMinute);
    }

    private RatDowngradeDetectionResult AnalyzeOrdered(IReadOnlyList<RadioObservation> observations)
    {
        TimeSpan windowDuration = CalculateWindowDuration(observations);
        RatObservation[] ratObservations = observations
            .Select(CreateRatObservation)
            .ToArray();
        MotionSummary motionSummary = SummarizeMotion(observations);
        RatDowngradeMetrics metrics = CalculateMetrics(ratObservations, windowDuration);
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

        return new RatDowngradeDetectionResult(
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
        RatDowngradeMetrics metrics)
    {
        if (observations.Count < _options.MinimumObservationCount)
        {
            return "suppressed:cold-start-observation-count";
        }

        if (windowDuration < _options.MinimumWindowDuration)
        {
            return "suppressed:cold-start-window-duration";
        }

        if (metrics.KnownRatObservationCount == 0)
        {
            return "suppressed:unknown-rat-history";
        }

        if (!metrics.HasLegacyDowngrade)
        {
            return "suppressed:no-legacy-downgrade";
        }

        if (metrics.ModernRatHistoryCount < _options.MinimumModernRatHistoryCount)
        {
            return "suppressed:insufficient-modern-rat-history";
        }

        if (motionSummary.IsBenignMobilityDominant)
        {
            return "suppressed:benign-motion-state";
        }

        if (!motionSummary.IsStationaryLike)
        {
            return "suppressed:insufficient-stationary-motion-evidence";
        }

        return null;
    }

    private double CalculateScore(RatDowngradeMetrics metrics)
    {
        double transitionScore = ScoreThreshold(
            metrics.DowngradeTransitionCount,
            _options.SuspiciousDowngradeCount,
            _options.HighRiskDowngradeCount);
        double rateScore = ScoreThreshold(
            metrics.DowngradesPerMinute,
            _options.SuspiciousDowngradesPerMinute,
            _options.HighRiskDowngradesPerMinute);
        double dwellScore = ScoreThreshold(
            metrics.LegacyDwellDuration.TotalSeconds,
            _options.SuspiciousLegacyDwellDuration.TotalSeconds,
            _options.HighRiskLegacyDwellDuration.TotalSeconds);
        double score = Math.Max(transitionScore, Math.Max(rateScore, dwellScore));

        if (metrics.HasGsmLegacyRat)
        {
            score = Math.Max(score, HighRiskScore);
        }

        if (metrics.IncludesNoServiceGap && score >= SuspiciousScore)
        {
            score += 0.08D;
        }

        if (metrics.ServiceFlapCount > 0 && score >= SuspiciousScore)
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

    private RatObservation CreateRatObservation(RadioObservation observation)
    {
        bool isServiceUnavailable = observation.ServiceState?.IsInService == false;
        RadioAccessTechnology radioAccessTechnology = ResolveRadioAccessTechnology(observation);
        bool isModern = _options.ModernRadioAccessTechnologies.Contains(radioAccessTechnology);
        bool isLegacy = _options.LegacyRadioAccessTechnologies.Contains(radioAccessTechnology);
        bool hasKnownRat = radioAccessTechnology != RadioAccessTechnology.Unknown;

        return new RatObservation(
            observation.ObservedAt,
            radioAccessTechnology,
            isServiceUnavailable,
            isModern,
            isLegacy,
            hasKnownRat);
    }

    private static RadioAccessTechnology ResolveRadioAccessTechnology(RadioObservation observation)
    {
        CellSnapshot? servingCell = observation.CellSnapshots.FirstOrDefault(
            static cell => cell.Role == CellRole.Serving || cell.IsRegistered == true);

        if (servingCell?.RadioAccessTechnology is { } servingRat
            && servingRat != RadioAccessTechnology.Unknown)
        {
            return servingRat;
        }

        if (observation.ServiceState?.DataRadioAccessTechnology is { } dataRat
            && dataRat != RadioAccessTechnology.Unknown)
        {
            return dataRat;
        }

        if (observation.ServiceState?.VoiceRadioAccessTechnology is { } voiceRat
            && voiceRat != RadioAccessTechnology.Unknown)
        {
            return voiceRat;
        }

        return RadioAccessTechnology.Unknown;
    }

    private static RatDowngradeMetrics CalculateMetrics(
        IReadOnlyList<RatObservation> ratObservations,
        TimeSpan windowDuration)
    {
        int firstLegacyIndex = FindFirstLegacyIndex(ratObservations);
        int modernRatHistoryCount = firstLegacyIndex < 0
            ? ratObservations.Count(static observation => observation.IsModern)
            : ratObservations.Take(firstLegacyIndex).Count(static observation => observation.IsModern);
        int legacyObservationCount = ratObservations.Count(static observation => observation.IsLegacy);
        int knownRatObservationCount = ratObservations.Count(static observation => observation.HasKnownRat);
        TimeSpan legacyDwellDuration = CalculateLegacyDwellDuration(ratObservations);
        int serviceFlapCount = CalculateServiceFlapCount(ratObservations);
        CompressedRatStep[] sequence = CreateCompressedSequence(ratObservations);
        DowngradeTransitionSummary transitionSummary = CalculateDowngradeTransitions(sequence);
        double downgradesPerMinute = windowDuration.TotalMinutes <= 0D
            ? 0D
            : transitionSummary.Count / windowDuration.TotalMinutes;
        string sequenceSummary = CreateSequenceSummary(sequence);
        bool hasGsmLegacyRat = ratObservations.Any(
            static observation => observation.RadioAccessTechnology == RadioAccessTechnology.Gsm);

        return new RatDowngradeMetrics(
            sequenceSummary,
            modernRatHistoryCount,
            legacyObservationCount,
            knownRatObservationCount,
            transitionSummary.Count,
            downgradesPerMinute,
            legacyDwellDuration,
            serviceFlapCount,
            transitionSummary.Count > 0,
            transitionSummary.IncludesNoServiceGap,
            hasGsmLegacyRat);
    }

    private static int FindFirstLegacyIndex(IReadOnlyList<RatObservation> ratObservations)
    {
        for (int index = 0; index < ratObservations.Count; index++)
        {
            if (ratObservations[index].IsLegacy)
            {
                return index;
            }
        }

        return -1;
    }

    private static TimeSpan CalculateLegacyDwellDuration(IReadOnlyList<RatObservation> ratObservations)
    {
        TimeSpan duration = TimeSpan.Zero;

        for (int index = 0; index < ratObservations.Count - 1; index++)
        {
            if (ratObservations[index].IsLegacy)
            {
                duration += ratObservations[index + 1].ObservedAt - ratObservations[index].ObservedAt;
            }
        }

        return duration;
    }

    private static int CalculateServiceFlapCount(IReadOnlyList<RatObservation> ratObservations)
    {
        int count = 0;

        for (int index = 1; index < ratObservations.Count; index++)
        {
            if (ratObservations[index - 1].IsServiceUnavailable != ratObservations[index].IsServiceUnavailable)
            {
                count++;
            }
        }

        return count;
    }

    private static CompressedRatStep[] CreateCompressedSequence(IReadOnlyList<RatObservation> ratObservations)
    {
        var sequence = new List<CompressedRatStep>();

        foreach (RatObservation observation in ratObservations)
        {
            RatStepKind kind = DetermineStepKind(observation);
            var step = new CompressedRatStep(kind, observation.RadioAccessTechnology);

            if (sequence.Count == 0 || sequence[^1] != step)
            {
                sequence.Add(step);
            }
        }

        return sequence.ToArray();
    }

    private static RatStepKind DetermineStepKind(RatObservation observation)
    {
        if (observation.IsServiceUnavailable)
        {
            return RatStepKind.NoService;
        }

        if (observation.IsModern)
        {
            return RatStepKind.Modern;
        }

        if (observation.IsLegacy)
        {
            return RatStepKind.Legacy;
        }

        return RatStepKind.Unknown;
    }

    private static DowngradeTransitionSummary CalculateDowngradeTransitions(
        IReadOnlyList<CompressedRatStep> sequence)
    {
        int count = 0;
        bool includesNoServiceGap = false;

        for (int index = 1; index < sequence.Count; index++)
        {
            CompressedRatStep current = sequence[index];

            if (current.Kind != RatStepKind.Legacy)
            {
                continue;
            }

            CompressedRatStep previous = sequence[index - 1];

            if (previous.Kind == RatStepKind.Modern)
            {
                count++;
                continue;
            }

            if (previous.Kind == RatStepKind.NoService && HasModernStepBefore(sequence, index - 1))
            {
                count++;
                includesNoServiceGap = true;
            }
        }

        return new DowngradeTransitionSummary(count, includesNoServiceGap);
    }

    private static bool HasModernStepBefore(IReadOnlyList<CompressedRatStep> sequence, int exclusiveEndIndex)
    {
        for (int index = exclusiveEndIndex - 1; index >= 0; index--)
        {
            if (sequence[index].Kind == RatStepKind.Modern)
            {
                return true;
            }

            if (sequence[index].Kind == RatStepKind.Legacy)
            {
                return false;
            }
        }

        return false;
    }

    private static string CreateSequenceSummary(IEnumerable<CompressedRatStep> sequence)
    {
        string[] values = sequence
            .Select(static step => step.Kind == RatStepKind.NoService
                ? "NoService"
                : step.RadioAccessTechnology.ToString())
            .ToArray();

        return values.Length == 0
            ? "empty"
            : string.Join(" -> ", values);
    }

    private static EvidenceBundle CreateEvidence(
        IReadOnlyList<RadioObservation> observations,
        TimeSpan windowDuration,
        MotionSummary motionSummary,
        RatDowngradeMetrics metrics,
        string? suppressionReason)
    {
        string windowSeconds = windowDuration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);
        string transitionRate = metrics.DowngradesPerMinute.ToString("F2", CultureInfo.InvariantCulture);
        string legacyDwellSeconds = metrics.LegacyDwellDuration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);
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
                "ObservedRatSequenceSummary",
                $"Observed RAT sequence: {metrics.ObservedRatSequenceSummary}.",
                metrics.DowngradeTransitionCount),
            CreateEvidenceItem(
                "ModernRatHistoryCount",
                $"Modern RAT history count before first legacy RAT: {metrics.ModernRatHistoryCount}.",
                metrics.ModernRatHistoryCount),
            CreateEvidenceItem(
                "LegacyRatObservationCount",
                $"Legacy RAT observation count: {metrics.LegacyRatObservationCount}.",
                metrics.LegacyRatObservationCount),
            CreateEvidenceItem(
                "DowngradeTransitionCount",
                $"Legacy downgrade transition count: {metrics.DowngradeTransitionCount}.",
                metrics.DowngradeTransitionCount),
            CreateEvidenceItem(
                "DowngradesPerMinute",
                $"Legacy downgrades per minute: {transitionRate}.",
                metrics.DowngradesPerMinute),
            CreateEvidenceItem(
                "LegacyDwellDuration",
                $"Legacy RAT dwell duration seconds: {legacyDwellSeconds}.",
                metrics.LegacyDwellDuration.TotalSeconds),
            CreateEvidenceItem(
                "ServiceFlapCount",
                $"Service availability flap count: {metrics.ServiceFlapCount}.",
                metrics.ServiceFlapCount),
            CreateEvidenceItem(
                "SuppressionReason",
                $"Suppression reason: {suppressionReason ?? "none"}.",
                suppressionReason is null ? 0D : 1D),
            CreateEvidenceItem(
                "LegacyDowngradeDetected",
                $"Legacy downgrade detected: {metrics.HasLegacyDowngrade}.",
                metrics.HasLegacyDowngrade ? 1D : 0D),
            CreateEvidenceItem(
                "NoServiceGapDetected",
                $"Pattern includes NoService gap: {metrics.IncludesNoServiceGap}.",
                metrics.IncludesNoServiceGap ? 1D : 0D)
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
        RatDowngradeMetrics metrics)
    {
        DateTimeOffset observedAt = observations.Count == 0
            ? DateTimeOffset.UnixEpoch
            : observations[^1].ObservedAt;

        return new AnomalyEvent(
            CreateEventId(evidence.BundleId),
            AnomalyType.SuspiciousDowngrade,
            evidence,
            observedAt,
            $"Suspicious RAT downgrade pattern: transitions={metrics.DowngradeTransitionCount}, legacyObservations={metrics.LegacyRatObservationCount}.");
    }

    private static string CreateBundleId(IReadOnlyList<RadioObservation> observations, RatDowngradeMetrics metrics)
    {
        long firstTicks = observations.Count == 0 ? 0L : observations[0].ObservedAt.UtcTicks;
        long lastTicks = observations.Count == 0 ? 0L : observations[^1].ObservedAt.UtcTicks;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"rat-downgrade-evidence:{firstTicks}:{lastTicks}:{metrics.DowngradeTransitionCount}:{metrics.LegacyRatObservationCount}");
    }

    private static string CreateEventId(string bundleId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"event:{bundleId}");
    }

    private static string CreateDecisionId(string bundleId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"decision:{bundleId}");
    }

    private sealed record RatObservation(
        DateTimeOffset ObservedAt,
        RadioAccessTechnology RadioAccessTechnology,
        bool IsServiceUnavailable,
        bool IsModern,
        bool IsLegacy,
        bool HasKnownRat);

    private sealed record RatDowngradeMetrics(
        string ObservedRatSequenceSummary,
        int ModernRatHistoryCount,
        int LegacyRatObservationCount,
        int KnownRatObservationCount,
        int DowngradeTransitionCount,
        double DowngradesPerMinute,
        TimeSpan LegacyDwellDuration,
        int ServiceFlapCount,
        bool HasLegacyDowngrade,
        bool IncludesNoServiceGap,
        bool HasGsmLegacyRat);

    private sealed record MotionSummary(
        int StationaryLikeCount,
        int BenignMobilityCount,
        int UnknownCount,
        int TotalCount)
    {
        public bool IsStationaryLike => StationaryLikeCount > 0 && StationaryLikeCount >= BenignMobilityCount;

        public bool IsBenignMobilityDominant => BenignMobilityCount > StationaryLikeCount;
    }

    private readonly record struct CompressedRatStep(
        RatStepKind Kind,
        RadioAccessTechnology RadioAccessTechnology);

    private readonly record struct DowngradeTransitionSummary(int Count, bool IncludesNoServiceGap);

    private enum RatStepKind
    {
        Unknown = 0,
        Modern = 1,
        Legacy = 2,
        NoService = 3
    }
}
