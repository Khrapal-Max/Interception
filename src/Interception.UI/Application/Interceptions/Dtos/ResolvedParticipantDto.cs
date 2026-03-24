//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Dtos;

/// <summary>Встановлена особа для відображення і редагування.</summary>
public sealed class ResolvedParticipantDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Role { get; init; }
    public string? Division { get; init; }
    public string ConfirmedBy { get; init; } = default!;
    public DateTime ConfirmedAt { get; init; }
}

/// <summary>Форма для підтвердження групи кандидатів.</summary>
public sealed class ConfirmCandidateGroupDto
{
    /// <summary>Встановлений позивний.</summary>
    public string Name { get; init; } = default!;

    /// <summary>Роль (необов'язково).</summary>
    public string? Role { get; init; }

    /// <summary>Підрозділ (необов'язково).</summary>
    public string? Division { get; init; }
}
