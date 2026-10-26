namespace Rpsp.Core.Scoring;

public sealed record RiskScore
{
    public RiskScore(double value, RiskSeverity severity, double confidence)
    {
        Value = ValidateUnitInterval(value, nameof(value));
        Severity = severity;
        Confidence = ValidateUnitInterval(confidence, nameof(confidence));
    }

    public double Value { get; }

    public RiskSeverity Severity { get; }

    public double Confidence { get; }

    private static double ValidateUnitInterval(double value, string parameterName)
    {
        if (double.IsNaN(value) || value < 0D || value > 1D)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }
}
