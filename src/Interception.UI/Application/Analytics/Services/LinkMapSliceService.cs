//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Services;

/// <summary>
/// Окремий сервіс зрізів поверх LinkMap.
/// Не змінює алгоритм побудови зв'язків, а лише дає зручну проекцію для UI.
/// </summary>
public sealed class LinkMapSliceService(ILinkMapService linkMapService) : ILinkMapSliceService
{
    private readonly ILinkMapService _linkMapService = linkMapService;

    /// <inheritdoc />
    public async Task<LinkMapSliceDto> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var map = await _linkMapService.BuildAsync(dateFrom, dateTo, ct);

        var groups = map.Groups
            .Select(group =>
            {
                var missingRoleCount = group.MemberDetails.Count(x => string.IsNullOrWhiteSpace(x.Role));
                var isDivisionMissing = string.IsNullOrWhiteSpace(group.Division);

                return new LinkMapGroupSliceDto(
                    group.GroupKey,
                    group.KeyPersonName,
                    group.Division,
                    isDivisionMissing,
                    group.Members.Count,
                    missingRoleCount,
                    group.MemberDetails.Count - missingRoleCount,
                    group.Members,
                    group.Frequencies,
                    group.TopActions);
            })
            .OrderByDescending(x => x.IsDivisionMissing)
            .ThenByDescending(x => x.MissingRoleCount)
            .ThenByDescending(x => x.MembersCount)
            .ThenBy(x => x.KeyPersonName)
            .ToList();

        return new LinkMapSliceDto(groups);
    }
}
