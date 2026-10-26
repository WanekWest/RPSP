using Rpsp.Core.Scoring;
using Xunit;

namespace Rpsp.Core.Tests;

public sealed class RiskScoreTests
{
    [Fact]
    public void ConstructorRejectsValuesBelowZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            static () => new RiskScore(-0.01D, RiskSeverity.Low, 0.5D));
    }

    [Fact]
    public void ConstructorRejectsValuesAboveOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            static () => new RiskScore(1.01D, RiskSeverity.High, 0.5D));
    }

    [Fact]
    public void ConstructorAcceptsBoundaryValues()
    {
        var zero = new RiskScore(0D, RiskSeverity.None, 0D);
        var one = new RiskScore(1D, RiskSeverity.Critical, 1D);

        Assert.Equal(0D, zero.Value);
        Assert.Equal(1D, one.Value);
    }
}
