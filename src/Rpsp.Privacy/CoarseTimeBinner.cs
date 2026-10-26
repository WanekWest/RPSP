namespace Rpsp.Privacy;

public sealed class CoarseTimeBinner
{
    private readonly TimeSpan _binSize;

    public CoarseTimeBinner(TimeSpan binSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(binSize, TimeSpan.Zero);

        _binSize = binSize;
    }

    public DateTimeOffset Bin(DateTimeOffset timestamp)
    {
        DateTimeOffset utcTimestamp = timestamp.ToUniversalTime();
        long ticks = utcTimestamp.UtcDateTime.Ticks;
        long binnedTicks = ticks - (ticks % _binSize.Ticks);
        var binnedUtc = new DateTime(binnedTicks, DateTimeKind.Utc);

        return new DateTimeOffset(binnedUtc);
    }
}
