using System.Globalization;
using Rpsp.Core.Baselines;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Scoring;

namespace Rpsp.Core.Detectors;

public sealed class UnknownStrongCellDetector
{
    private const double SuspiciousScore = 0.6D;
    private const double HighRiskScore = 0.8D;
    private readonly UnknownStrongCellDetectorOptions _options;

    public UnknownStrongCellDetector(UnknownStrongCellDetectorOptions? options = null)
    {
        _options = options ?? new UnknownStrongCellDetectorOptions();
        ValidateOptions(_options);
    }

    public UnknownStrongCellDetectionResult Analyze(
        IEnumerable<RadioObservation> observations,
        BaselineProfile baseline)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(baseline);

        return Analyze(observations, baseline.TrustedCells);
    }

    public UnknownStrongCellDetectionResult Analyze(
        IEnumerable<RadioObservation> observations,
        IEnumerable<TrustedCellSignature> trustedCells)
    {
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(trustedCells);

        RadioObservation[] orderedObservations = observations
            .OrderBy(static observation => observation.ObservedAt)
            .ToArray();
        TrustedCellSignature[] trustedCellSignatures = trustedCells.ToArray();

        return AnalyzeOrdered(orderedObservations, trustedCellSignatures);
    }

    public async ValueTask<UnknownStrongCellDetectionResult> AnalyzeAsync(
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

    private static void ValidateOptions(UnknownStrongCellDetectorOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumObservationCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.MinimumWindowDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumTrustedBaselineCellCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            options.VeryStrongRsrpThresholdDbm,
            options.StrongRsrpThresholdDbm);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumUnknownStrongAppearances, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.SuspiciousUnknownStrongServingAppearances, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            options.HighRiskUnknownStrongServingAppearances,
            options.SuspiciousUnknownStrongServingAppearances);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.SuspiciousUnknownStrongNeighborAppearances, 1);
        ValidateUnitInterval(options.StrongSignalScoreWeight, nameof(options.StrongSignalScoreWeight));
        ValidateUnitInterval(options.ServingCellScoreWeight, nameof(options.ServingCellScoreWeight));
        ValidateUnitInterval(options.RepeatedAppearanceScoreWeight, nameof(options.RepeatedAppearanceScoreWeight));
    }

    private UnknownStrongCellDetectionResult AnalyzeOrdered(
        IReadOnlyList<RadioObservation> observations,
        TrustedCellSignature[] trustedCells)
    {
        TimeSpan windowDuration = CalculateWindowDuration(observations);
        MotionSummary motionSummary = SummarizeMotion(observations);
        CellObservation[] cellObservations = CreateCellObservations(observations, trustedCells);
        UnknownStrongCellMetrics metrics = CalculateMetrics(cellObservations, windowDuration, trustedCells.Length);
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

        return new UnknownStrongCellDetectionResult(
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
        UnknownStrongCellMetrics metrics)
    {
        if (observations.Count < _options.MinimumObservationCount)
        {
            return "suppressed:cold-start-observation-count";
        }

        if (windowDuration < _options.MinimumWindowDuration)
        {
            return "suppressed:cold-start-window-duration";
        }

        if (metrics.TrustedBaselineCellCount < _options.MinimumTrustedBaselineCellCount)
        {
            return "suppressed:insufficient-trusted-baseline";
        }

        if (metrics.ObservedCellCount == 0)
        {
            return "suppressed:no-observed-cells";
        }

        if (motionSummary.IsBenignMobilityDominant)
        {
            return "suppressed:benign-motion-state";
        }

        if (!motionSummary.IsStationaryLike)
        {
            return "suppressed:insufficient-stationary-motion-evidence";
        }

        if (metrics.UnknownCellCount == 0)
        {
            return "suppressed:no-unknown-cells";
        }

        if (metrics.UnknownStrongCellCount == 0)
        {
            return "suppressed:no-strong-signal-evidence";
        }

        if (metrics.UnknownStrongCellCount < _options.MinimumUnknownStrongAppearances)
        {
            return "suppressed:insufficient-unknown-strong-appearances";
        }

        return null;
    }

    private double CalculateScore(UnknownStrongCellMetrics metrics)
    {
        double servingScore = ScoreThreshold(
            metrics.UnknownStrongServingAppearances,
            _options.SuspiciousUnknownStrongServingAppearances,
            _options.HighRiskUnknownStrongServingAppearances) * _options.ServingCellScoreWeight;
        double neighborScore = metrics.UnknownStrongServingAppearances == 0
            ? ScoreNeighborOnly(metrics.UnknownStrongNeighborAppearances)
            : 0D;
        double score = Math.Max(servingScore, neighborScore);

        if (metrics.HasRepeatedUnknownStrongServingCell && score >= SuspiciousScore)
        {
            score += _options.RepeatedAppearanceScoreWeight;
        }

        if (metrics.IsVeryStrongUnknownServingObserved && score >= SuspiciousScore)
        {
            score += _options.StrongSignalScoreWeight;
        }

        return Math.Clamp(score, 0D, 1D);
    }

    private double ScoreNeighborOnly(int unknownStrongNeighborAppearances)
    {
        if (unknownStrongNeighborAppearances < _options.SuspiciousUnknownStrongNeighborAppearances)
        {
            return 0D;
        }

        int extraAppearances = unknownStrongNeighborAppearances - _options.SuspiciousUnknownStrongNeighborAppearances;
        double extraScore = Math.Min(extraAppearances * 0.03D, 0.10D);

        return Math.Min(0.45D + extraScore, 0.55D);
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

    private static CellObservation[] CreateCellObservations(
        IReadOnlyList<RadioObservation> observations,
        IReadOnlyList<TrustedCellSignature> trustedCells)
    {
        var cellObservations = new List<CellObservation>();

        foreach (RadioObservation observation in observations)
        {
            foreach (CellSnapshot cell in observation.CellSnapshots)
            {
                bool isTrusted = trustedCells.Any(trustedCell => IsTrustedMatch(cell, trustedCell));
                string cellKey = CreateCellKey(cell);
                bool isServing = cell.Role == CellRole.Serving || cell.IsRegistered == true;
                bool isNeighbor = cell.Role == CellRole.Neighbor && !isServing;

                cellObservations.Add(new CellObservation(
                    observation.ObservedAt,
                    cellKey,
                    isServing,
                    isNeighbor,
                    cell.Signal?.RsrpDbm,
                    isTrusted));
            }
        }

        return cellObservations.ToArray();
    }

    private static bool IsTrustedMatch(CellSnapshot cell, TrustedCellSignature trustedCell)
    {
        if (cell.RadioAccessTechnology != trustedCell.RadioAccessTechnology)
        {
            return false;
        }

        CellIdentifier? identifier = cell.Identifier;
        bool networkCellMatch = !string.IsNullOrWhiteSpace(trustedCell.NetworkCellPseudonym)
            && !string.IsNullOrWhiteSpace(identifier?.NetworkCellId)
            && string.Equals(trustedCell.NetworkCellPseudonym, identifier.NetworkCellId, StringComparison.Ordinal);
        bool physicalAndChannelMatch = trustedCell.PhysicalCellId is not null
            && trustedCell.ChannelNumber is not null
            && trustedCell.PhysicalCellId == identifier?.PhysicalCellId
            && trustedCell.ChannelNumber == identifier?.ChannelNumber;

        return networkCellMatch || physicalAndChannelMatch;
    }

    private UnknownStrongCellMetrics CalculateMetrics(
        IReadOnlyList<CellObservation> cells,
        TimeSpan windowDuration,
        int trustedBaselineCellCount)
    {
        CellObservation[] unknownCells = cells
            .Where(static cell => !cell.IsTrusted)
            .ToArray();
        CellObservation[] unknownStrongCells = unknownCells
            .Where(cell => IsStrongSignal(cell.RsrpDbm))
            .ToArray();
        int unknownStrongServingAppearances = unknownStrongCells.Count(static cell => cell.IsServing);
        int unknownStrongNeighborAppearances = unknownStrongCells.Count(static cell => cell.IsNeighbor);
        int observedCellCount = cells.Count;
        double? strongestObservedRsrp = cells
            .Where(static cell => cell.RsrpDbm is not null)
            .Select(static cell => cell.RsrpDbm!.Value)
            .DefaultIfEmpty(double.NaN)
            .Max();
        bool hasSignal = !double.IsNaN(strongestObservedRsrp.Value);
        int maximumRepeatedServingAppearances = unknownStrongCells
            .Where(static cell => cell.IsServing)
            .GroupBy(static cell => cell.CellKey, StringComparer.Ordinal)
            .Select(static group => group.Count())
            .DefaultIfEmpty(0)
            .Max();
        bool repeatedServing = maximumRepeatedServingAppearances >= _options.MinimumUnknownStrongAppearances;
        bool veryStrongServing = unknownStrongCells.Any(
            cell => cell.IsServing && IsVeryStrongSignal(cell.RsrpDbm));

        return new UnknownStrongCellMetrics(
            trustedBaselineCellCount,
            observedCellCount,
            unknownCells.Length,
            unknownStrongCells.Length,
            unknownStrongServingAppearances,
            unknownStrongNeighborAppearances,
            hasSignal ? strongestObservedRsrp : null,
            _options.StrongRsrpThresholdDbm,
            windowDuration.TotalMinutes <= 0D
                ? 0D
                : unknownStrongCells.Length / windowDuration.TotalMinutes,
            repeatedServing,
            veryStrongServing,
            trustedBaselineCellCount >= _options.MinimumTrustedBaselineCellCount);
    }

    private bool IsStrongSignal(double? rsrpDbm)
    {
        return rsrpDbm >= _options.StrongRsrpThresholdDbm;
    }

    private bool IsVeryStrongSignal(double? rsrpDbm)
    {
        return rsrpDbm >= _options.VeryStrongRsrpThresholdDbm;
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
        UnknownStrongCellMetrics metrics,
        string? suppressionReason)
    {
        string windowSeconds = windowDuration.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture);
        string strongestRsrp = metrics.StrongestObservedRsrpDbm?.ToString("F1", CultureInfo.InvariantCulture) ?? "unavailable";
        string threshold = metrics.SignalThresholdUsedDbm.ToString("F1", CultureInfo.InvariantCulture);
        string unknownStrongRate = metrics.UnknownStrongAppearancesPerMinute.ToString("F2", CultureInfo.InvariantCulture);
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
                "TrustedBaselineCellCount",
                $"Trusted baseline cell count: {metrics.TrustedBaselineCellCount}.",
                metrics.TrustedBaselineCellCount),
            CreateEvidenceItem(
                "ObservedCellCount",
                $"Observed cell count: {metrics.ObservedCellCount}.",
                metrics.ObservedCellCount),
            CreateEvidenceItem(
                "UnknownCellCount",
                $"Unknown cell count: {metrics.UnknownCellCount}.",
                metrics.UnknownCellCount),
            CreateEvidenceItem(
                "UnknownStrongCellCount",
                $"Unknown strong cell count: {metrics.UnknownStrongCellCount}.",
                metrics.UnknownStrongCellCount),
            CreateEvidenceItem(
                "UnknownStrongServingAppearances",
                $"Unknown strong serving appearances: {metrics.UnknownStrongServingAppearances}.",
                metrics.UnknownStrongServingAppearances),
            CreateEvidenceItem(
                "UnknownStrongNeighborAppearances",
                $"Unknown strong neighbor appearances: {metrics.UnknownStrongNeighborAppearances}.",
                metrics.UnknownStrongNeighborAppearances),
            CreateEvidenceItem(
                "StrongestObservedRsrp",
                $"Strongest observed RSRP dBm: {strongestRsrp}.",
                metrics.StrongestObservedRsrpDbm is null ? 0D : 1D),
            CreateEvidenceItem(
                "SignalThresholdUsed",
                $"Strong RSRP threshold dBm: {threshold}.",
                Math.Abs(metrics.SignalThresholdUsedDbm)),
            CreateEvidenceItem(
                "UnknownStrongAppearancesPerMinute",
                $"Unknown strong appearances per minute: {unknownStrongRate}.",
                metrics.UnknownStrongAppearancesPerMinute),
            CreateEvidenceItem(
                "BaselineSufficiency",
                $"Trusted baseline sufficient: {metrics.IsTrustedBaselineSufficient}.",
                metrics.IsTrustedBaselineSufficient ? 1D : 0D),
            CreateEvidenceItem(
                "RepeatedUnknownStrongServingCellObserved",
                $"Repeated unknown strong serving cell observed: {metrics.HasRepeatedUnknownStrongServingCell}.",
                metrics.HasRepeatedUnknownStrongServingCell ? 1D : 0D),
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
        UnknownStrongCellMetrics metrics)
    {
        DateTimeOffset observedAt = observations.Count == 0
            ? DateTimeOffset.UnixEpoch
            : observations[^1].ObservedAt;

        return new AnomalyEvent(
            CreateEventId(evidence.BundleId),
            AnomalyType.NewStrongUnknownCell,
            evidence,
            observedAt,
            $"Unknown strong cell observation: servingAppearances={metrics.UnknownStrongServingAppearances}, neighborAppearances={metrics.UnknownStrongNeighborAppearances}.");
    }

    private static string CreateBundleId(IReadOnlyList<RadioObservation> observations, UnknownStrongCellMetrics metrics)
    {
        long firstTicks = observations.Count == 0 ? 0L : observations[0].ObservedAt.UtcTicks;
        long lastTicks = observations.Count == 0 ? 0L : observations[^1].ObservedAt.UtcTicks;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"unknown-strong-cell-evidence:{firstTicks}:{lastTicks}:{metrics.UnknownStrongCellCount}:{metrics.UnknownStrongServingAppearances}:{metrics.UnknownStrongNeighborAppearances}");
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

    private sealed record CellObservation(
        DateTimeOffset ObservedAt,
        string CellKey,
        bool IsServing,
        bool IsNeighbor,
        double? RsrpDbm,
        bool IsTrusted);

    private sealed record UnknownStrongCellMetrics(
        int TrustedBaselineCellCount,
        int ObservedCellCount,
        int UnknownCellCount,
        int UnknownStrongCellCount,
        int UnknownStrongServingAppearances,
        int UnknownStrongNeighborAppearances,
        double? StrongestObservedRsrpDbm,
        double SignalThresholdUsedDbm,
        double UnknownStrongAppearancesPerMinute,
        bool HasRepeatedUnknownStrongServingCell,
        bool IsVeryStrongUnknownServingObserved,
        bool IsTrustedBaselineSufficient);

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
