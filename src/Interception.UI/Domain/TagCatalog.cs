//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Reference catalog for reusable tags/keywords.
/// Observation keeps raw tags, but can optionally bind them to catalog items.
/// </summary>
public sealed class TagCatalog
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = default!;
    public string NameNorm { get; private set; } = default!;
    public TagKind Kind { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }

    public List<ObservationTag> ObservationTags { get; private set; } = [];

    private TagCatalog()
    {
    }

    public static TagCatalog Create(string name, TagKind kind, string? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name is required.", nameof(name));

        return new TagCatalog
        {
            Name = name.Trim(),
            NameNorm = TextNorm.NormalizeRequired(name),
            Kind = kind,
            CreatedBy = NormalizeOptional(createdBy),
            IsActive = true
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name is required.", nameof(name));

        Name = name.Trim();
        NameNorm = TextNorm.NormalizeRequired(name);
    }

    public void ChangeKind(TagKind kind)
    {
        Kind = kind;
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
