using Rpsp.Core.Observations;

namespace Rpsp.Core.Baselines;

public sealed record TrustedCellSignature(
    string SignatureId,
    RadioAccessTechnology RadioAccessTechnology,
    string? PlmnPseudonym = null,
    string? NetworkCellPseudonym = null,
    int? PhysicalCellId = null,
    int? ChannelNumber = null,
    DateTimeOffset? FirstTrustedAt = null,
    DateTimeOffset? LastSeenAt = null);
