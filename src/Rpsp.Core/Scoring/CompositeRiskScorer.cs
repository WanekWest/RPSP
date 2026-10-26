using System.Globalization;
using Rpsp.Core.Detectors;
using Rpsp.Core.Events;
using Rpsp.Core.Observations;

namespace Rpsp.Core.Scoring;

public sealed class CompositeRiskScorer
{
    private const double SuspiciousScore = 0.6D;
    private const double HighRiskScore = 0.8D;
    private readonly CompositeRiskScorerOptions _options;

    public CompositeRiskScorer(CompositeRiskScorerOptions? options = null)
    {
        _options = options ?? new CompositeRiskScorerOptions();
        ValidateOptions(_options);
    }

    public CompositeRiskAssessment Assess(
        StationaryHandoverDetectionResult stationaryHandover,
        RatDowngradeDetectionResult ratDowngrade)
    {
        ArgumentNullException.ThrowIfNull(stationaryHandover);
        ArgumentNullException.ThrowIfNull(ratDowngrade);

        return Assess(
        [
            DetectorRiskContribution.FromStationaryHandover(stationaryHandover),
            DetectorRiskContribution.FromRatDowngrade(ratDowngrade)
        ]);
    }

    public CompositeRiskAssessment Assess(IEnumerable<DetectorRiskContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        DetectorRiskContribution[] allContributions = contributions.ToArray();
        DetectorRiskContribution[] reportContributions = _options.IncludeSuppressedContributionsInReport
            ? allContributions
            : allContributions.Where(static contribution => !contribution.IsSuppressed).ToArray();
        DetectorRiskContribution[] activeContributions = allContributions
            .Where(contribution => IsActiveContribution(contribution, _options.MinimumContributionScore))
            .OrderByDescending(static contribution => contribution.RiskScore.Value)
            .ThenBy(static contribution => contribution.DetectorName, StringComparer.Ordinal)
            .ToArray();
        DetectorRiskContribution? highestContribution = activeContributions.FirstOrDefault();
        double baseScore = highestContribution?.RiskScore.Value ?? 0D;
        double correlationBoost = CalculateCorrelationBoost(activeContributions.Skip(1));
        double finalScore = Math.Clamp(baseScore + correlationBoost, 0D, 1D);
        RiskSeverity severity = SelectSeverity(finalScore, activeContributions);
        WarningLevel warningLevel = ToWarningLevel(severity);
        EvidenceBundle evidence = CreateEvidence(
            allContributions,
            activeContributions,
            reportContributions,
            highestContribution,
            baseScore,
            correlationBoost,
            finalScore);
        var riskScore = new RiskScore(finalScore, severity, CalculateConfidence(allContributions, activeContributions));
        var warningDecision = new WarningDecision(
            CreateDecisionId(evidence.BundleId),
            warningLevel,
            riskScore);

        return new CompositeRiskAssessment(
            evidence,
            riskScore,
            warningDecision,
            reportContributions,
            allContributions.Length,
            activeContributions.Length,
            allContributions.Count(static contribution => contribution.IsSuppressed),
            highestContribution?.DetectorName,
            baseScore,
            correlationBoost);
    }

    private static void ValidateOptions(CompositeRiskScorerOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(options.AdditionalSuspiciousBoost);
        ArgumentOutOfRangeException.ThrowIfNegative(options.AdditionalHighRiskBoost);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaximumCorrelationBoost);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MinimumContributionScore);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.MinimumContributionScore, 1D);
    }

    private static bool IsActiveContribution(DetectorRiskContribution contribution, double minimumScore)
    {
        return !contribution.IsSuppressed
            && contribution.RiskScore.Value >= minimumScore
            && contribution.RiskScore.Severity != RiskSeverity.None;
    }

    private double CalculateCorrelationBoost(IEnumerable<DetectorRiskContribution> additionalContributions)
    {
        double boost = additionalContributions
            .Where(static contribution => contribution.RiskScore.Value >= SuspiciousScore)
            .Sum(contribution => contribution.RiskScore.Value >= HighRiskScore
                || contribution.RiskScore.Severity >= RiskSeverity.High
                    ? _options.AdditionalHighRiskBoost
                    : _options.AdditionalSuspiciousBoost);

        return Math.Clamp(boost, 0D, _options.MaximumCorrelationBoost);
    }

    private static RiskSeverity SelectSeverity(
        double finalScore,
        DetectorRiskContribution[] activeContributions)
    {
        RiskSeverity scoreSeverity = ToSeverity(finalScore);
        RiskSeverity highestContributionSeverity = activeContributions.Length == 0
            ? RiskSeverity.None
            : activeContributions.Max(static contribution => contribution.RiskScore.Severity);

        return highestContributionSeverity > scoreSeverity
            ? highestContributionSeverity
            : scoreSeverity;
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

    private static double CalculateConfidence(
        DetectorRiskContribution[] allContributions,
        DetectorRiskContribution[] activeContributions)
    {
        if (activeContributions.Length > 0)
        {
            return Math.Clamp(activeContributions.Max(static contribution => contribution.RiskScore.Confidence), 0D, 1D);
        }

        return allContributions.Length == 0
            ? 1D
            : Math.Clamp(allContributions.Max(static contribution => contribution.RiskScore.Confidence), 0D, 1D);
    }

    private static EvidenceBundle CreateEvidence(
        DetectorRiskContribution[] allContributions,
        DetectorRiskContribution[] activeContributions,
        DetectorRiskContribution[] reportContributions,
        DetectorRiskContribution? highestContribution,
        double baseScore,
        double correlationBoost,
        double finalScore)
    {
        int suppressedCount = allContributions.Count(static contribution => contribution.IsSuppressed);
        string baseScoreText = baseScore.ToString("F3", CultureInfo.InvariantCulture);
        string boostText = correlationBoost.ToString("F3", CultureInfo.InvariantCulture);
        string finalScoreText = finalScore.ToString("F3", CultureInfo.InvariantCulture);
        EvidenceItem[] items =
        [
            CreateEvidenceItem(
                "TotalDetectorCount",
                $"Total detector count: {allContributions.Length}.",
                allContributions.Length),
            CreateEvidenceItem(
                "ActiveContributionCount",
                $"Active contribution count: {activeContributions.Length}.",
                activeContributions.Length),
            CreateEvidenceItem(
                "SuppressedContributionCount",
                $"Suppressed contribution count: {suppressedCount}.",
                suppressedCount),
            CreateEvidenceItem(
                "HighestContributingDetector",
                $"Highest contributing detector: {highestContribution?.DetectorName ?? "none"}.",
                highestContribution is null ? 0D : 1D),
            CreateEvidenceItem(
                "BaseScore",
                $"Composite base score: {baseScoreText}.",
                baseScore),
            CreateEvidenceItem(
                "CorrelationBoost",
                $"Composite correlation boost: {boostText}.",
                correlationBoost),
            CreateEvidenceItem(
                "FinalScore",
                $"Composite final score: {finalScoreText}.",
                finalScore),
            CreateEvidenceItem(
                "DetectorContributionSummary",
                $"Report contribution count: {reportContributions.Length}.",
                reportContributions.Length)
        ];

        return new EvidenceBundle(
            CreateBundleId(allContributions, baseScore, correlationBoost, finalScore),
            items,
            MergeObservations(allContributions));
    }

    private static EvidenceItem CreateEvidenceItem(string name, string description, double weight)
    {
        return new EvidenceItem(name, description, ObservationAvailability.Derived, Math.Clamp(weight, 0D, 1D));
    }

    private static RadioObservation[] MergeObservations(
        IEnumerable<DetectorRiskContribution> contributions)
    {
        return contributions
            .SelectMany(static contribution => contribution.Evidence.Observations)
            .GroupBy(
                static observation => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{observation.ObservationId}:{observation.ObservedAt.UtcTicks}"),
                StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static observation => observation.ObservedAt)
            .ToArray();
    }

    private static string CreateBundleId(
        IReadOnlyCollection<DetectorRiskContribution> contributions,
        double baseScore,
        double correlationBoost,
        double finalScore)
    {
        string contributionKeys = string.Join(
            '|',
            contributions
                .OrderBy(static contribution => contribution.DetectorName, StringComparer.Ordinal)
                .Select(static contribution => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{contribution.DetectorName}:{contribution.Evidence.BundleId}:{contribution.RiskScore.Value:F3}:{contribution.IsSuppressed}")));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"composite-risk-evidence:{contributionKeys}:{baseScore:F3}:{correlationBoost:F3}:{finalScore:F3}");
    }

    private static string CreateDecisionId(string bundleId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"decision:{bundleId}");
    }
}
