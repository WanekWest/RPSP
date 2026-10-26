namespace Rpsp.Core.Baselines;

public sealed record BaselineProfile
{
    public BaselineProfile(
        string profileId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        IReadOnlyList<TrustedCellSignature> trustedCells)
    {
        ArgumentNullException.ThrowIfNull(trustedCells);

        ProfileId = profileId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        TrustedCells = trustedCells;
    }

    public string ProfileId { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public IReadOnlyList<TrustedCellSignature> TrustedCells { get; }
}
