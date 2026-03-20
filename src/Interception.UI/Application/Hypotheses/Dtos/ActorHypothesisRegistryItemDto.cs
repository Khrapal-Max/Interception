//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Lightweight row for person hypothesis registry.
/// </summary>
public sealed record ActorHypothesisRegistryItemDto(
    Guid Id,
    string Title,
    string? Note,
    int MembersCount,
    Guid? ResolvedActorId,
    string? ResolvedActorDisplayName,
    string? ResolvedActorPrimaryRole,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc,
    DateTime? LastSeenAtUtc);
