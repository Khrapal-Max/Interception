//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain;

namespace Interception.UI.Application.Interceptions.Abstractions;

/// <summary>Контракт основного сервісу перехоплень: CRUD + suggestions.</summary>
public interface IInterceptionService
{
    // -------------------------------------------------------------------------
    // Suggestions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Suggestions для поля Частота.
    /// Повертає пари Частота + найчастіший Підрозділ + найчастіший Вектор.
    /// </summary>
    Task<IReadOnlyList<FrequencySuggestionDto>> GetFrequencyWithDivisionAsync(
        string? query = null, int take = 10, CancellationToken ct = default);

    /// <summary>
    /// Suggestions для поля Вектор сигналу.
    /// Якщо передано <paramref name="frequency"/> — повертає тільки вектори
    /// що зустрічались з цією частотою (контекстний список після вибору частоти).
    /// Якщо frequency = null — звичайний пошук по query (Contains).
    /// </summary>
    Task<IReadOnlyList<string>> GetVectorSignalSuggestionsAsync(
        string? query = null, string? frequency = null,
        int take = 10, CancellationToken ct = default);

    Task<IReadOnlyList<ParticipantSuggestionDto>> GetParticipantSuggestionsAsync(
        string? query = null, int take = 15, CancellationToken ct = default);

    // -------------------------------------------------------------------------
    // CRUD
    // -------------------------------------------------------------------------

    Task<PagedResult<InterceptionListItemDto>> GetPagedAsync(
        InterceptionFilter filter, int page = 1, int pageSize = 50,
        CancellationToken ct = default);

    Task<InterceptionMessage?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<InterceptionMessage> CreateAsync(
        InterceptionFormDto form, string operatorName, CancellationToken ct = default);

    Task UpdateAsync(Guid id, InterceptionFormDto form, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
