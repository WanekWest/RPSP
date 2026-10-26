using System.Text;
using Rpsp.Core.Observations;
using Rpsp.Privacy;
using Xunit;

namespace Rpsp.Privacy.Tests;

public sealed class HmacCellIdentifierPseudonymizerTests
{
    [Fact]
    public void SameIdentifierAndKeyProduceSamePseudonym()
    {
        byte[] key = Encoding.UTF8.GetBytes("local-secret-key-one");
        var identifier = CreateIdentifier();
        var pseudonymizer = new HmacCellIdentifierPseudonymizer(key);

        CellIdentifierPseudonym first = pseudonymizer.Pseudonymize(identifier);
        CellIdentifierPseudonym second = pseudonymizer.Pseudonymize(identifier);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SameIdentifierAndDifferentKeysProduceDifferentPseudonyms()
    {
        var identifier = CreateIdentifier();
        var first = new HmacCellIdentifierPseudonymizer(
            Encoding.UTF8.GetBytes("local-secret-key-one"));
        var second = new HmacCellIdentifierPseudonymizer(
            Encoding.UTF8.GetBytes("local-secret-key-two"));

        Assert.NotEqual(first.Pseudonymize(identifier), second.Pseudonymize(identifier));
    }

    [Fact]
    public void PseudonymDoesNotContainRawNetworkCellValue()
    {
        var identifier = CreateIdentifier();
        var pseudonymizer = new HmacCellIdentifierPseudonymizer(
            Encoding.UTF8.GetBytes("local-secret-key-one"));

        CellIdentifierPseudonym pseudonym = pseudonymizer.Pseudonymize(identifier);

        Assert.DoesNotContain(identifier.NetworkCellId!, pseudonym.Value, StringComparison.Ordinal);
    }

    private static CellIdentifier CreateIdentifier()
    {
        return new CellIdentifier(
            new Plmn("001", "01"),
            "raw-cell-alpha",
            PhysicalCellId: 42,
            TrackingAreaCode: 100,
            ChannelNumber: 6300);
    }
}
