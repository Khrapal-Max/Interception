//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Interceptions.Abstractions;

/// <summary>
/// Read-only контекстні підказки для операторського вводу перехоплень.
/// </summary>
public interface IInterceptionSuggestionService
{
    /// <summary>
    /// Повертає підказки для частоти з типовим підрозділом і вектором.
    /// </summary>
    Task<IReadOnlyList<FrequencySuggestionDto>> GetFrequencyWithDivisionAsync(
        string? query = null,
        int take = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Повертає підказки для вектора сигналу.
    /// </summary>
    Task<IReadOnlyList<string>> GetVectorSignalSuggestionsAsync(
        string? query = null,
        string? frequency = null,
        int take = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Повертає підказки для відомих учасників.
    /// </summary>
    Task<IReadOnlyList<ParticipantSuggestionDto>> GetParticipantSuggestionsAsync(
        string? query = null,
        int take = 15,
        CancellationToken ct = default);
}
