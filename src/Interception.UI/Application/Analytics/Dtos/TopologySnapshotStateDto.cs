//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Стан snapshot-а топології для вибраного періоду.
/// </summary>
public sealed record TopologySnapshotStateDto(
    bool HasSnapshot,
    bool IsStale,
    bool IsBuilding,
    string Status,
    DateTime? LastCompletedAt,
    int GroupCount,
    string? ErrorMessage);
