//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Registry.Dtos;

/// <summary>
/// Елемент довідника дій для відображення в UI.
/// </summary>
public sealed class InterceptionActionListItemDto
{
    /// <summary>Ідентифікатор дії.</summary>
    public Guid Id { get; init; }

    /// <summary>Назва дії.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Опис дії.</summary>
    public string Description { get; init; } = string.Empty;
}
