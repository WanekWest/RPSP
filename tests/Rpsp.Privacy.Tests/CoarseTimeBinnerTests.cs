using Rpsp.Privacy;
using Xunit;

namespace Rpsp.Privacy.Tests;

public sealed class CoarseTimeBinnerTests
{
    [Fact]
    public void BinRoundsTimestampDownToConfiguredBinSize()
    {
        var binner = new CoarseTimeBinner(TimeSpan.FromMinutes(15D));
        var timestamp = new DateTimeOffset(2026, 5, 15, 12, 34, 56, TimeSpan.Zero);

        DateTimeOffset binned = binner.Bin(timestamp);

        Assert.Equal(new DateTimeOffset(2026, 5, 15, 12, 30, 0, TimeSpan.Zero), binned);
    }
}
