//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Керує побудовою та станом snapshot-ів топології карти зв'язків.
/// </summary>
public interface ITopologySnapshotBuilder
{
    /// <summary>
    /// Повертає стан snapshot-а для вибраного періоду.
    /// </summary>
    Task<TopologySnapshotStateDto> GetStateAsync(
        DateTime? dateFromUtc = null,
        DateTime? dateToUtc = null,
        CancellationToken ct = default);

    /// <summary>
    /// Перебудовує snapshot для вибраного періоду.
    /// </summary>
    Task<TopologySnapshotRebuildResultDto> RebuildAsync(
        DateTime? dateFromUtc = null,
        DateTime? dateToUtc = null,
        CancellationToken ct = default);
}
