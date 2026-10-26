namespace Rpsp.Core.Observations;

public sealed record SignalSnapshot(
    double? RssiDbm = null,
    double? RsrpDbm = null,
    double? RsrqDb = null,
    double? RssnrDb = null,
    double? SinrDb = null,
    int? AsuLevel = null,
    int? Level = null,
    int? TimingAdvance = null);
