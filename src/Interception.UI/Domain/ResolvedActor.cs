//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Confirmed or stable actor identity resolved from one or more unknown participant observations.
/// </summary>
public sealed class ResolvedActor
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string DisplayName { get; private set; } = default!;
    public string DisplayNameNorm { get; private set; } = default!;
    public string? PrimaryRole { get; private set; }
    public string? Note { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }

    public List<UnknownCluster> ResolvedClusters { get; private set; } = [];

    private ResolvedActor()
    {
    }

    public static ResolvedActor Create(string displayName, string? primaryRole = null, string? note = null, string? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        return new ResolvedActor
        {
            DisplayName = displayName.Trim(),
            DisplayNameNorm = TextNorm.NormalizeRequired(displayName),
            PrimaryRole = NormalizeOptional(primaryRole),
            Note = NormalizeOptional(note),
            CreatedBy = NormalizeOptional(createdBy),
            IsActive = true
        };
    }

    public void Rename(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        DisplayName = displayName.Trim();
        DisplayNameNorm = TextNorm.NormalizeRequired(displayName);
    }

    public void UpdatePrimaryRole(string? primaryRole)
    {
        PrimaryRole = NormalizeOptional(primaryRole);
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
