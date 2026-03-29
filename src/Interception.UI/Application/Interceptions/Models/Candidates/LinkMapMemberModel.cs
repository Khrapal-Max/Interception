//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Models.Candidates;

/// <summary>
/// Детальний вузол-учасник усередині комунікаційної групи.
/// </summary>
public sealed record LinkMapMemberModel(
    string Name,
    string? Role,
    int MentionCount,
    int UniquePartnerCount,
    int ConnectionWeight,
    DateTime LastSeenAt,
    int GroupCount,
    bool IsCrossGroup,
    bool IsKeyPerson);
