//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

// ---------------------------------------------------------------------------
// Suggestion (автопідстановка)
// ---------------------------------------------------------------------------

/// <summary>
/// Пропозиція учасника для autocomplete поля.
/// Один label може відповідати кільком профілям,
/// тому suggestion групується не тільки по імені, а по частоті та підрозділу.
/// </summary>
public sealed class ParticipantSuggestionDto
{
    /// <summary>
    /// Ім'я / позивний учасника.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// Частота, на якій ця комбінація найчастіше зустрічалась.
    /// </summary>
    public string? Frequency { get; init; }

    /// <summary>
    /// Підрозділ / Р/М, у якому ця комбінація найчастіше зустрічалась.
    /// </summary>
    public string? Division { get; init; }

    /// <summary>
    /// Роль, яка найчастіше зустрічалась для конкретної комбінації.
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// Скільки разів ця комбінація зустрічалась в історії.
    /// </summary>
    public int SeenCount { get; init; }
}
