//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Сервіс non-person suggestions: підрозділ / контекст / середовище групи.
/// </summary>
public interface IContextSuggestionService
{
    /// <summary>
    /// Повертає top-N контекстних підказок для групи кандидатів.
    /// </summary>
    Task<IReadOnlyList<CandidateContextSuggestionDto>> GetContextSuggestionsAsync(
        Guid groupId,
        int take = 3,
        CancellationToken ct = default);
}
