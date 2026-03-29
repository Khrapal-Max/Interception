//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Models.Candidates;

/// <summary>
/// Деталізований вузол-учасник у межах комунікаційної групи.
/// </summary>
public sealed record LinkMapMemberModel(
    string Name,
    string? Role,
    int Mentions,
    int UniquePartnerCount,
    int ConnectionWeight,
    DateTime LastSeenAt,
    int GroupCount,
    bool IsCrossGroup,
    bool IsKeyPerson);
