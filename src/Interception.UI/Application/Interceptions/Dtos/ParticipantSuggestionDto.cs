//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Dtos;

// ---------------------------------------------------------------------------
// Suggestion (автопідстановка)
// ---------------------------------------------------------------------------

/// <summary>
/// Пропозиція учасника для autocomplete поля.
/// </summary>
public sealed class ParticipantSuggestionDto
{
    public string Name { get; init; } = default!;

    /// <summary>Остання відома роль — підставляється автоматично.</summary>
    public string? Role { get; init; }
}
