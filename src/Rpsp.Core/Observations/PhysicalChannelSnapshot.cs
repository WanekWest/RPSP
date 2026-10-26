namespace Rpsp.Core.Observations;

public sealed record PhysicalChannelSnapshot(
    RadioAccessTechnology? RadioAccessTechnology = null,
    int? DownlinkChannelNumber = null,
    int? UplinkChannelNumber = null,
    int? CellBandwidthKhz = null,
    int? PhysicalCellId = null);
