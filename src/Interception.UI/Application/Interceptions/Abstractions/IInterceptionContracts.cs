//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain;

namespace Interception.UI.Application.Interceptions.Abstractions;

/// <summary>Контракт основного сервісу перехоплень: CRUD + suggestions.</summary>
public interface IInterceptionService
{
    Task<IReadOnlyList<string>> GetFrequencySuggestionsAsync(
        string? query = null, int take = 10, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetVectorSignalSuggestionsAsync(
        string? query = null, int take = 10, CancellationToken ct = default);

    Task<IReadOnlyList<ParticipantSuggestionDto>> GetParticipantSuggestionsAsync(
        string? query = null, int take = 15, CancellationToken ct = default);

    Task<PagedResult<InterceptionListItemDto>> GetPagedAsync(
        InterceptionFilter filter, int page = 1, int pageSize = 50, CancellationToken ct = default);

    Task<InterceptionMessage?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<InterceptionMessage> CreateAsync(
        InterceptionFormDto form, string operatorName, CancellationToken ct = default);

    Task UpdateAsync(Guid id, InterceptionFormDto form, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
