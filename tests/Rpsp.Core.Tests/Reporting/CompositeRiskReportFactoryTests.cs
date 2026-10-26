using Rpsp.Core.Events;
using Rpsp.Core.Observations;
using Rpsp.Core.Reporting;
using Rpsp.Core.Scoring;
using Xunit;

namespace Rpsp.Core.Tests.Reporting;

public sealed class CompositeRiskReportFactoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CompositeReportIsDeterministic()
    {
        CompositeRiskAssessment assessment = CreateSuspiciousAssessment();
        var formatter = new PlainTextDetectorReportFormatter();

        string first = formatter.Format(CompositeRiskReportFactory.Create(assessment));
        string second = formatter.Format(CompositeRiskReportFactory.Create(assessment));

        Assert.Equal(first, second);
    }

    [Fact]
    public void CompositeReportContainsContributionSummary()
    {
        CompositeRiskAssessment assessment = CreateSuspiciousAssessment();
        string report = Format(assessment);

        Assert.Contains("Composite risk assessment", report, StringComparison.Ordinal);
        Assert.Contains("Decision: Suspicious", report, StringComparison.Ordinal);
        Assert.Contains("Detector Contributions", report, StringComparison.Ordinal);
        Assert.Contains("StationaryHandoverAnomalyDetector", report, StringComparison.Ordinal);
        Assert.Contains("RatDowngradeRiskDetector", report, StringComparison.Ordinal);
        Assert.Contains("combined anomaly indicators", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompositeReportOmitsRawCellIdentifiersByDefault()
    {
        CompositeRiskAssessment assessment = CreateSuspiciousAssessment();
        string report = Format(assessment);

        Assert.DoesNotContain("synthetic-cell-alpha", report, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-cell-beta", report, StringComparison.Ordinal);
    }

    [Fact]
    public void CompositeReportDoesNotContainForbiddenClaimPhrases()
    {
        CompositeRiskAssessment assessment = CreateSuspiciousAssessment();
        string report = Format(assessment);
        string[] forbiddenPhrases =
        [
            string.Concat("attack", " confirmed"),
            string.Concat("IM", "SI", "-catcher detected"),
            string.Concat("lawful", " intercept", " detection"),
            string.Concat("definitive", " IM", "SI", "-catcher identification")
        ];

        foreach (string phrase in forbiddenPhrases)
        {
            Assert.DoesNotContain(phrase, report, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CompositeReportDoesNotContainSubscriberOrDeviceIdentifierTerms()
    {
        CompositeRiskAssessment assessment = CreateSuspiciousAssessment();
        string report = Format(assessment);
        string[] restrictedTerms =
        [
            string.Concat("IM", "SI"),
            string.Concat("IM", "EI"),
            string.Concat("MS", "IS", "DN"),
            string.Concat("SU", "PI"),
            string.Concat("SU", "CI")
        ];

        foreach (string term in restrictedTerms)
        {
            Assert.DoesNotContain(term, report, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string Format(CompositeRiskAssessment assessment)
    {
        var formatter = new PlainTextDetectorReportFormatter();

        return formatter.Format(CompositeRiskReportFactory.Create(assessment));
    }

    private static CompositeRiskAssessment CreateSuspiciousAssessment()
    {
        var scorer = new CompositeRiskScorer();

        return scorer.Assess(
        [
            CreateContribution("StationaryHandoverAnomalyDetector", 0.65D, RiskSeverity.Medium, "synthetic-cell-alpha"),
            CreateContribution("RatDowngradeRiskDetector", 0D, RiskSeverity.None, "synthetic-cell-beta", true, "suppressed:no-legacy-downgrade")
        ]);
    }

    private static DetectorRiskContribution CreateContribution(
        string detectorName,
        double score,
        RiskSeverity severity,
        string rawCellId,
        bool isSuppressed = false,
        string? suppressionReason = null)
    {
        var riskScore = new RiskScore(score, severity, confidence: 0.8D);
        var evidence = new EvidenceBundle(
            string.Create(null, $"evidence:{detectorName}:{score:F3}:{isSuppressed}"),
            [new EvidenceItem("SyntheticEvidence", "Synthetic detector evidence for composite report tests.", ObservationAvailability.Derived, score)],
            [CreateObservation(detectorName, rawCellId)]);

        return new DetectorRiskContribution(
            detectorName,
            riskScore,
            ToWarningLevel(severity),
            evidence,
            isSuppressed,
            suppressionReason);
    }

    private static RadioObservation CreateObservation(string detectorName, string rawCellId)
    {
        var serving = new CellSnapshot(
            CellRole.Serving,
            RadioAccessTechnology.Lte,
            new CellIdentifier(new Plmn("001", "01"), rawCellId, PhysicalCellId: 42),
            new SignalSnapshot(RsrpDbm: -82D),
            ObservationAvailability.Available,
            IsRegistered: true);

        return new RadioObservation(
            string.Create(null, $"observation:{detectorName}"),
            Start,
            ObservationSourceKind.Replay,
            [serving]);
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
}
