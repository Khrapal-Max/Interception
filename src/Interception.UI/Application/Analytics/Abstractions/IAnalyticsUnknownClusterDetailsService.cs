//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Сервіс сторінки деталей unknown-кластера.
/// Працює в аналітичному шарі та не змінює raw-спостереження.
/// </summary>
public interface IAnalyticsUnknownClusterDetailsService
{
    /// <summary>
    /// Повертає повний DTO для сторінки деталей кластера.
    /// </summary>
    Task<AnalyticsUnknownClusterDetailsPageDto?> GetPageAsync(Guid clusterId, CancellationToken ct);

    /// <summary>
    /// Шукає кластери для merge або reassign, крім поточного.
    /// </summary>
    Task<IReadOnlyList<AnalyticsUnknownClusterLookupDto>> SearchOtherClustersAsync(Guid clusterId, string query, int take, CancellationToken ct);

    /// <summary>
    /// Резолвить поточний кластер у встановлену особу.
    /// </summary>
    Task ResolveClusterAsActorAsync(Guid clusterId, string displayName, string? callsign, string? note, CancellationToken ct);

    /// <summary>
    /// Переносить усіх учасників поточного кластера в інший кластер та позначає поточний як merged.
    /// </summary>
    Task MergeClusterAsync(Guid clusterId, Guid targetClusterId, string? reason, CancellationToken ct);

    /// <summary>
    /// Перепризначає одного raw-учасника з поточного кластера в інший кластер.
    /// </summary>
    Task ReassignParticipantAsync(Guid clusterId, Guid participantId, Guid targetClusterId, string? reason, CancellationToken ct);
}
