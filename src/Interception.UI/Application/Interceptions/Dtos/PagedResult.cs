//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Dtos;

// ---------------------------------------------------------------------------
// Пагінація
// ---------------------------------------------------------------------------

public sealed class PagedResult<T>(
    IReadOnlyList<T> items,
    int totalCount,
    int page,
    int pageSize)
{
    public IReadOnlyList<T> Items { get; } = items;
    public int TotalCount { get; } = totalCount;
    public int Page { get; } = page;
    public int PageSize { get; } = pageSize;
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
