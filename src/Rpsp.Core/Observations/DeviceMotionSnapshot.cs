namespace Rpsp.Core.Observations;

public sealed record DeviceMotionSnapshot
{
    public DeviceMotionSnapshot(
        DeviceMotionState state,
        double confidence,
        double? speedMetersPerSecond = null)
    {
        State = state;
        Confidence = ValidateUnitInterval(confidence, nameof(confidence));
        SpeedMetersPerSecond = speedMetersPerSecond;
    }

    public DeviceMotionState State { get; }

    public double Confidence { get; }

    public double? SpeedMetersPerSecond { get; }

    private static double ValidateUnitInterval(double value, string parameterName)
    {
        if (double.IsNaN(value) || value < 0D || value > 1D)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }
}
