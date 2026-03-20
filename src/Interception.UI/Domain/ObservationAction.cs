//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Typed observation action from the reference catalog.
/// Gives a stable action type on top of noisy raw action text.
/// </summary>
public sealed class ObservationAction
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Official action name for UI and registries.
    /// </summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Normalized name for search and matching.
    /// </summary>
    public string NameNorm { get; private set; } = default!;

    public ObservationActionCategory Category { get; private set; } = ObservationActionCategory.Other;

    /// <summary>
    /// Human-readable role of initiator for this action type.
    /// </summary>
    public string? InitiatorRoleName { get; private set; }

    /// <summary>
    /// Human-readable role of responder/target for this action type.
    /// </summary>
    public string? ResponderRoleName { get; private set; }

    /// <summary>
    /// Short description or operator hint.
    /// </summary>
    public string? Description { get; private set; }

    public short? TypicalParticipantsCount { get; private set; }
    public bool RequiresCounterparty { get; private set; }
    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }

    public List<Observation> Observations { get; private set; } = [];
    public List<ObservationProbableAction> ProbableActions { get; private set; } = [];

    private ObservationAction()
    {
    }

    public static ObservationAction Create(
        string name,
        ObservationActionCategory category = ObservationActionCategory.Other,
        string? initiatorRoleName = null,
        string? responderRoleName = null,
        string? description = null,
        short? typicalParticipantsCount = null,
        bool requiresCounterparty = false,
        string? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Action name is required.", nameof(name));
        if (typicalParticipantsCount is <= 0)
            throw new ArgumentOutOfRangeException(nameof(typicalParticipantsCount), "Typical participants count must be >= 1.");

        var normalizedName = TextNorm.NormalizeRequired(name);

        return new ObservationAction
        {
            Name = name.Trim(),
            NameNorm = normalizedName,
            Category = category,
            InitiatorRoleName = NormalizeOptional(initiatorRoleName),
            ResponderRoleName = NormalizeOptional(responderRoleName),
            Description = NormalizeOptional(description),
            TypicalParticipantsCount = typicalParticipantsCount,
            RequiresCounterparty = requiresCounterparty,
            CreatedBy = NormalizeOptional(createdBy),
            IsActive = true
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Action name is required.", nameof(name));

        Name = name.Trim();
        NameNorm = TextNorm.NormalizeRequired(name);
    }

    public void SetCategory(ObservationActionCategory category)
    {
        Category = category;
    }

    public void ConfigureRoles(string? initiatorRoleName, string? responderRoleName)
    {
        InitiatorRoleName = NormalizeOptional(initiatorRoleName);
        ResponderRoleName = NormalizeOptional(responderRoleName);
    }

    public void ConfigureUsage(short? typicalParticipantsCount, bool requiresCounterparty)
    {
        if (typicalParticipantsCount is <= 0)
            throw new ArgumentOutOfRangeException(nameof(typicalParticipantsCount), "Typical participants count must be >= 1.");

        TypicalParticipantsCount = typicalParticipantsCount;
        RequiresCounterparty = requiresCounterparty;
    }

    public void SetDescription(string? description)
    {
        Description = NormalizeOptional(description);
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
