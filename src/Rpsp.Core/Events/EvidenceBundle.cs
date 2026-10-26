using Rpsp.Core.Observations;

namespace Rpsp.Core.Events;

public sealed record EvidenceBundle
{
    public EvidenceBundle(
        string bundleId,
        IReadOnlyList<EvidenceItem> items,
        IReadOnlyList<RadioObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(observations);

        BundleId = bundleId;
        Items = items;
        Observations = observations;
    }

    public string BundleId { get; }

    public IReadOnlyList<EvidenceItem> Items { get; }

    public IReadOnlyList<RadioObservation> Observations { get; }
}
