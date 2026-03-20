//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Lightweight lookup item for open person hypotheses.
/// </summary>
public sealed record UnknownClusterLookupDto(
    Guid Id,
    string Title,
    int MembersCount,
    bool IsResolved,
    bool IsArchived,
    string? ResolvedActorDisplayName);
