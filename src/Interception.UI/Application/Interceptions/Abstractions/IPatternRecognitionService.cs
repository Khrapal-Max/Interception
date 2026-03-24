//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Interceptions.Abstractions;

public interface IPatternRecognitionService
{
    /// <summary>
    /// Запускає аналіз і створює/збагачує групи кандидатів.
    ///
    /// Правила:
    ///   — Confirmed НВ → ніколи не розглядаються
    ///   — Open НВ      → нова група не створюється, але нові НВ
    ///                    можуть збагатити існуючу Open групу
    ///   — Dismissed НВ → розглядаються завжди ("замало інфи")
    ///
    /// Повертає кількість створених або збагачених груп.
    /// </summary>
    Task<int> RunAsync(CancellationToken ct = default);

    /// <summary>
    /// Повертає групи за статусом з пагінацією,
    /// відсортовані за ConfidenceScore DESC.
    /// </summary>
    Task<PagedResult<CandidateGroupDto>> GetGroupsByStatusAsync(
        CandidateGroupStatus status,
        int page = 1, int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>Повертає групу за Id з деталями спостережень.</summary>
    Task<CandidateGroupDto?> GetGroupByIdAsync(
        Guid id, CancellationToken ct = default);

    /// <summary>
    /// Для групи кандидатів повертає топ відомих учасників
    /// що за ознаками (частота, вектор, підрозділ, мітки, час)
    /// схожі на НВ з цієї групи.
    ///
    /// Викликається при відкритті дравера — не під час RunAsync.
    /// </summary>
    Task<IReadOnlyList<KnownParticipantSuggestionDto>> GetKnownSuggestionsAsync(
        Guid groupId,
        int take = 3,
        CancellationToken ct = default);

    /// <summary>
    /// Для групи кандидатів повертає не конкретних осіб, а ймовірний контекст
    /// належності: підрозділ / середовище, в якому ця НВ-група стабільно
    /// з'являється. Допомагає тоді, коли до конкретної особи ще зарано.
    /// </summary>
    Task<IReadOnlyList<CandidateContextSuggestionDto>> GetContextSuggestionsAsync(
        Guid groupId,
        int take = 3,
        CancellationToken ct = default);
}
