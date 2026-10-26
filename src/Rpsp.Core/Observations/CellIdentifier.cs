namespace Rpsp.Core.Observations;

public sealed record CellIdentifier(
    Plmn? Plmn,
    string? NetworkCellId,
    int? PhysicalCellId = null,
    int? TrackingAreaCode = null,
    int? ChannelNumber = null);
