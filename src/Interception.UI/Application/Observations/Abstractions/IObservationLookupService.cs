//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Dtos;

namespace Interception.UI.Application.Observations.Abstractions;

/// <summary>
/// Lookup/suggestion service for observation input flows.
/// </summary>
public interface IObservationLookupService
{
    Task<IReadOnlyList<LayerRmSuggestionDto>> GetLayerSuggestionsAsync(string query, int take, CancellationToken ct);

    Task<IReadOnlyList<string>> GetDistrictSuggestionsAsync(string query, int take, CancellationToken ct);

    Task<IReadOnlyList<ParticipantSuggestionDto>> SearchParticipantSuggestionsAsync(string query, int take, CancellationToken ct);

    Task<IReadOnlyList<ActionTextSuggestionDto>> SearchActionTextSuggestionsAsync(string query, int take, CancellationToken ct);

    Task<IReadOnlyList<ActionCatalogSuggestionDto>> SearchActionCatalogSuggestionsAsync(string query, int take, CancellationToken ct);

    Task<IReadOnlyList<SubdivisionSuggestionDto>> SearchSubdivisionSuggestionsAsync(string query, int take, CancellationToken ct);
}
