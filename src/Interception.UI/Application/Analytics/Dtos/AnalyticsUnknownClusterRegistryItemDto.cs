//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Рядок реєстру аналітичних кластерів.
/// </summary>
public sealed record AnalyticsUnknownClusterRegistryItemDto(
    Guid Id,
    string Code,
    string? DisplayName,
    string? Role,
    string Status,
    string? ArchiveReason,
    int ParticipantsCount,
    DateOnly? LastSeenDate,
    string? LinkedActorDisplayName,
    string? ConfirmedRole);