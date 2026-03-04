//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Raw participant snapshot in a single observation (append-only).
/// </summary>
public class ObservationParticipant
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ObservationId { get; private set; }
    public Observation Observation { get; private set; } = default!;

    /// <summary>
    /// Operator-entered label (callsign/name). Can be null/empty if unknown.
    /// Example: "КЛИМ", "ТАШКЕНТ", "НВ".
    /// </summary>
    public string? LabelRaw { get; private set; }

    /// <summary>
    /// Normalized label for grouping (trim + collapse whitespace + lower).
    /// Null if <see cref="LabelRaw"/> is null/empty.
    /// </summary>
    public string? LabelNorm { get; private set; }

    /// <summary>
    /// True when the participant is unknown (НВ).
    /// </summary>
    public bool IsUnknown { get; private set; }

    /// <summary>
    /// Optional raw role as entered by operator (can be unknown/empty).
    /// </summary>
    public string? RoleRaw { get; private set; }

    /// <summary>
    /// Participant order in the original record (1..N). Helps preserve source formatting.
    /// </summary>
    public int Ordinal { get; private set; }

    private ObservationParticipant() { } // EF

    internal ObservationParticipant(Guid observationId, string? labelRaw, bool isUnknown, string? roleRaw, int ordinal)
    {
        if (ordinal <= 0) throw new ArgumentOutOfRangeException(nameof(ordinal), "Ordinal must be >= 1.");

        ObservationId = observationId;
        LabelRaw = string.IsNullOrWhiteSpace(labelRaw) ? null : labelRaw.Trim();
        LabelNorm = TextNorm.Normalize(LabelRaw);
        IsUnknown = isUnknown || LabelNorm is null; // if label missing, treat as unknown
        RoleRaw = string.IsNullOrWhiteSpace(roleRaw) ? null : roleRaw.Trim();
        Ordinal = ordinal;
    }
}
