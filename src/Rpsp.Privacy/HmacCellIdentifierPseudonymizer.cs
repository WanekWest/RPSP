using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Rpsp.Core.Observations;

namespace Rpsp.Privacy;

public sealed class HmacCellIdentifierPseudonymizer
{
    private readonly byte[] _key;

    public HmacCellIdentifierPseudonymizer(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length == 0)
        {
            throw new ArgumentException(null, nameof(key));
        }

        _key = key.ToArray();
    }

    public CellIdentifierPseudonym Pseudonymize(CellIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        byte[] payload = Encoding.UTF8.GetBytes(ToCanonicalString(identifier));
        byte[] hash = HMACSHA256.HashData(_key, payload);

        return new CellIdentifierPseudonym(Convert.ToHexString(hash));
    }

    private static string ToCanonicalString(CellIdentifier identifier)
    {
        string[] parts =
        [
            identifier.Plmn?.MobileCountryCode ?? string.Empty,
            identifier.Plmn?.MobileNetworkCode ?? string.Empty,
            identifier.NetworkCellId ?? string.Empty,
            ToInvariantString(identifier.PhysicalCellId),
            ToInvariantString(identifier.TrackingAreaCode),
            ToInvariantString(identifier.ChannelNumber)
        ];

        return string.Join('\u001F', parts);
    }

    private static string ToInvariantString(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
