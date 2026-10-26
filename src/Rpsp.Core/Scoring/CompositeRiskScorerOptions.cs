namespace Rpsp.Core.Scoring;

public sealed record CompositeRiskScorerOptions
{
    public double AdditionalSuspiciousBoost { get; init; } = 0.10D;

    public double AdditionalHighRiskBoost { get; init; } = 0.15D;

    public double MaximumCorrelationBoost { get; init; } = 0.25D;

    public double MinimumContributionScore { get; init; } = 0.01D;

    public bool IncludeSuppressedContributionsInReport { get; init; } = true;
}
