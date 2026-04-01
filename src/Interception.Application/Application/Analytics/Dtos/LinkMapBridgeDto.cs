//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Міст між двома комунікаційними групами.
/// </summary>
public sealed record LinkMapBridgeDto(
    string TargetGroupKey,
    string? TargetDivision,
    string ContactPersonName,
    string BridgeFrequency,
    int Weight,
    string? PrimaryAction,
    IReadOnlyList<string> TopActions);
