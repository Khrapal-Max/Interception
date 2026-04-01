//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Сервіс підказок по конкретних відомих особах для групи невідомих учасників.
/// </summary>
public interface IKnownParticipantSuggestionService
{
    /// <summary>
    /// Повертає top-N підказок по відомих учасниках.
    /// </summary>
    Task<IReadOnlyList<KnownParticipantSuggestionDto>> GetKnownSuggestionsAsync(
        Guid groupId,
        int take = 3,
        CancellationToken ct = default);
}
