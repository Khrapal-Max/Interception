//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Детальний вузол-учасник усередині комунікаційної групи.
/// </summary>
public sealed record LinkMapMemberDto(
    string Name,
    string? Role,
    int MentionCount,
    int UniquePartnerCount,
    int ConnectionWeight,
    DateTime LastSeenAt,
    int GroupCount,
    bool IsCrossGroup,
    bool IsKeyPerson);
