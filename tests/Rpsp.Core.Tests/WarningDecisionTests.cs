using Rpsp.Core.Events;
using Rpsp.Core.Scoring;
using Xunit;

namespace Rpsp.Core.Tests;

public sealed class WarningDecisionTests
{
    [Fact]
    public void DecisionReflectsSeverityAndLevel()
    {
        var score = new RiskScore(0.82D, RiskSeverity.High, 0.9D);
        var decision = new WarningDecision("decision-1", WarningLevel.Warning, score);

        Assert.Equal(WarningLevel.Warning, decision.Level);
        Assert.Equal(RiskSeverity.High, decision.Severity);
        Assert.Same(score, decision.RiskScore);
    }
}
