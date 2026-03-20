//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Confirmed subdivision identity resolved from weak or unknown observation hints.
/// </summary>
public sealed class ResolvedSubdivision
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = default!;
    public string NameNorm { get; private set; } = default!;
    public string? LayerHint { get; private set; }
    public string? RmHint { get; private set; }
    public string? Note { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }

    public List<UnknownSubdivisionCluster> ResolvedClusters { get; private set; } = [];

    private ResolvedSubdivision()
    {
    }

    public static ResolvedSubdivision Create(
        string name,
        string? layerHint = null,
        string? rmHint = null,
        string? note = null,
        string? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Subdivision name is required.", nameof(name));

        return new ResolvedSubdivision
        {
            Name = name.Trim(),
            NameNorm = TextNorm.NormalizeRequired(name),
            LayerHint = NormalizeOptional(layerHint),
            RmHint = NormalizeOptional(rmHint),
            Note = NormalizeOptional(note),
            CreatedBy = NormalizeOptional(createdBy),
            IsActive = true
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Subdivision name is required.", nameof(name));

        Name = name.Trim();
        NameNorm = TextNorm.NormalizeRequired(name);
    }

    public void UpdateHints(string? layerHint, string? rmHint)
    {
        LayerHint = NormalizeOptional(layerHint);
        RmHint = NormalizeOptional(rmHint);
    }

    public void SetNote(string? note)
    {
        Note = NormalizeOptional(note);
    }

    public void Archive()
    {
        IsActive = false;
    }

    public void Restore()
    {
        IsActive = true;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
