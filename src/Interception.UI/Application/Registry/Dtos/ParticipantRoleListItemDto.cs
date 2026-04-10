//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Registry.Dtos;

/// <summary>
/// Елемент довідника ролей для відображення в UI.
/// </summary>
public sealed class ParticipantRoleListItemDto
{
    /// <summary>Ідентифікатор ролі.</summary>
    public Guid Id { get; init; }

    /// <summary>Назва ролі.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Опис ролі.</summary>
    public string Description { get; init; } = string.Empty;
}
