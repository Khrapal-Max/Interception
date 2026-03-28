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
    /// Ідентифікатор canonical person або raw participant row.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Позивний / назва особи.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Канонічна або ефективна роль.
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// Канонічний або ефективний підрозділ.
    /// </summary>
    public string? Division { get; init; }

    /// <summary>
    /// Ознака confirmed person.
    /// </summary>
    public bool IsConfirmed { get; init; }

    /// <summary>
    /// Хто підтвердив особу, якщо вона canonical.
    /// </summary>
    public string? ConfirmedBy { get; init; }

    /// <summary>
    /// Коли підтверджено canonical person.
    /// </summary>
    public DateTime? ConfirmedAt { get; init; }
}
