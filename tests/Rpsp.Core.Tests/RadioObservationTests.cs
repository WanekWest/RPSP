using Rpsp.Core.Observations;
using Xunit;

namespace Rpsp.Core.Tests;

public sealed class RadioObservationTests
{
    [Fact]
    public void ObservationCanContainServingAndNeighborSnapshotsWithoutPlatformDependencies()
    {
        var serving = new CellSnapshot(
            CellRole.Serving,
            RadioAccessTechnology.Lte,
            new CellIdentifier(new Plmn("001", "01"), "serving-cell", PhysicalCellId: 42),
            new SignalSnapshot(RsrpDbm: -82D),
            ObservationAvailability.Available,
            IsRegistered: true);

        var neighbor = new CellSnapshot(
            CellRole.Neighbor,
            RadioAccessTechnology.Lte,
            new CellIdentifier(new Plmn("001", "01"), "neighbor-cell", PhysicalCellId: 43),
            new SignalSnapshot(RsrpDbm: -87D),
            ObservationAvailability.Available);

        var observation = new RadioObservation(
            "obs-1",
            DateTimeOffset.UtcNow,
            ObservationSourceKind.StockCore,
            [serving, neighbor]);

        Assert.Contains(observation.CellSnapshots, static cell => cell.Role == CellRole.Serving);
        Assert.Contains(observation.CellSnapshots, static cell => cell.Role == CellRole.Neighbor);
    }
}
