//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Dtos;

/// <summary>
/// Рядок реєстру осіб.
/// </summary>
public sealed class PersonRegistryItemDto
{
    /// <summary>
    /// Ідентифікатор особи або рядка спостереження.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Ім'я або позивний.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Роль особи.
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// Підрозділ особи.
    /// </summary>
    public string? Division { get; init; }

    /// <summary>
    /// Ознака канонічної підтвердженої особи.
    /// </summary>
    public bool IsConfirmed { get; init; }
}
