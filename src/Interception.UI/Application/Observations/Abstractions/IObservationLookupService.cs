//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Dtos;

namespace Interception.UI.Application.Observations.Abstractions;

/// <summary>
/// Provides lookup suggestions for manual observation entry.
/// </summary>
public interface IObservationLookupService
{
    /// <summary>
    /// Returns layer suggestions with matching R/M values.
    /// Selecting a result should fill both Layer and R/M.
    /// </summary>
    Task<IReadOnlyList<LayerRmSuggestionDto>> GetLayerSuggestionsAsync(string query, int take, CancellationToken ct);

    /// <summary>
    /// Returns district suggestions. Operator may ignore them and type manually.
    /// </summary>
    Task<IReadOnlyList<string>> GetDistrictSuggestionsAsync(string query, int take, CancellationToken ct);

    /// <summary>
    /// Returns participant suggestions based on previous observations.
    /// </summary>
    Task<IReadOnlyList<ParticipantSuggestionDto>> SearchParticipantSuggestionsAsync(string query, int take, CancellationToken ct);

    /// <summary>
    /// Returns short action context for the current action and already selected participants.
    /// </summary>
    Task<IReadOnlyList<ActionContextSuggestionDto>> GetActionContextAsync(
        string? actionRaw,
        IReadOnlyCollection<string> participantKeys,
        int take,
        CancellationToken ct);
}
