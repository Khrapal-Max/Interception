//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Models.Candidates;

/// <summary>
/// Комунікаційна група.
/// </summary>
public sealed record LinkMapGroupModel(
    string GroupKey,
    string? Division,
    IReadOnlyList<string> Frequencies,
    string KeyPersonName,
    string? KeyPersonRole,
    IReadOnlyList<string> Members,
    IReadOnlyList<LinkMapMemberModel> MemberDetails,
    int MentionCount,
    int InternalConnectionWeight,
    int BridgeWeight,
    IReadOnlyList<LinkMapBridgeModel> Bridges);
