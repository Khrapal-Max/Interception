//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Результат ручної перебудови snapshot-а топології.
/// </summary>
public sealed record TopologySnapshotRebuildResultDto(
    Guid RunId,
    int GroupCount,
    DateTime CompletedAtUtc);
