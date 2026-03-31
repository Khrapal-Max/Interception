//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Read-side сервіс для сторінок реєстру та деталей груп кандидатів.
/// </summary>
public interface IParticipantCandidateGroupQueryService
{
    /// <summary>
    /// Повертає групи кандидатів за статусом із пагінацією.
    /// </summary>
    Task<PagedResult<CandidateGroupDto>> GetGroupsByStatusAsync(
        CandidateGroupStatus status,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Повертає одну групу кандидатів з деталями observation refs.
    /// </summary>
    Task<CandidateGroupDto?> GetGroupByIdAsync(Guid id, CancellationToken ct = default);
}
