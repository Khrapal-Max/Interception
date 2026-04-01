//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Registry.Dtos;

/// <summary>
/// Модель редагування особи в реєстрі.
/// </summary>
public sealed class PersonRegistryUpdateDto
{
    /// <summary>
    /// Ім'я або позивний.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Роль.
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// Підрозділ.
    /// </summary>
    public string? Division { get; init; }
}
