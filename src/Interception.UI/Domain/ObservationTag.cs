//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Raw or bound keyword/tag attached to a specific observation.
/// </summary>
public sealed class ObservationTag
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ObservationId { get; private set; }
    public Observation Observation { get; private set; } = default!;

    public Guid? TagCatalogId { get; private set; }
    public TagCatalog? TagCatalog { get; private set; }

    public string RawValue { get; private set; } = default!;
    public string RawValueNorm { get; private set; } = default!;
    public TagKind Kind { get; private set; }
    public ObservationTagSource Source { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private ObservationTag()
    {
    }

    internal static ObservationTag Create(
        Guid observationId,
        string rawValue,
        TagKind kind,
        ObservationTagSource source,
        Guid? tagCatalogId = null)
    {
        if (observationId == Guid.Empty)
            throw new ArgumentException("Observation id is required.", nameof(observationId));
        if (string.IsNullOrWhiteSpace(rawValue))
            throw new ArgumentException("Tag value is required.", nameof(rawValue));

        return new ObservationTag
        {
            ObservationId = observationId,
            TagCatalogId = tagCatalogId,
            RawValue = rawValue.Trim(),
            RawValueNorm = TextNorm.NormalizeRequired(rawValue),
            Kind = kind,
            Source = source
        };
    }

    public void Update(string rawValue, TagKind kind, Guid? tagCatalogId = null)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            throw new ArgumentException("Tag value is required.", nameof(rawValue));

        RawValue = rawValue.Trim();
        RawValueNorm = TextNorm.NormalizeRequired(rawValue);
        Kind = kind;
        TagCatalogId = tagCatalogId;
    }

    public void BindCatalog(Guid tagCatalogId)
    {
        if (tagCatalogId == Guid.Empty)
            throw new ArgumentException("Tag catalog id is required.", nameof(tagCatalogId));

        TagCatalogId = tagCatalogId;
    }

    public void ClearCatalog()
    {
        TagCatalogId = null;
    }
}
