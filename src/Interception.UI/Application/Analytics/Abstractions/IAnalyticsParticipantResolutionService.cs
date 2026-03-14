//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Analytics.Dtos.AnalyticsUnknownClusters;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Сервіс аналітичної резолюції raw-учасників спостережень.
/// Працює окремо від операторського контуру і не змінює raw-історію спостережень.
/// </summary>
public interface IAnalyticsParticipantResolutionService
{
    /// <summary>
    /// Повертає повний контекст для аналітичної сторінки учасника.
    /// </summary>
    Task<AnalyticsParticipantResolutionPageDto?> GetPageAsync(Guid participantId, CancellationToken ct);

    /// <summary>
    /// Шукає наявні unknown-кластери для додавання raw-учасника.
    /// </summary>
    Task<IReadOnlyList<AnalyticsUnknownClusterLookupDto>> SearchUnknownClustersAsync(string query, int take, CancellationToken ct);

    /// <summary>
    /// Створює новий unknown-кластер і додає до нього raw-учасника.
    /// </summary>
    Task CreateUnknownClusterAsync(Guid participantId, string? displayName, string? role, string? reason, CancellationToken ct);

    /// <summary>
    /// Додає raw-учасника в існуючий unknown-кластер.
    /// Якщо прив'язка вже існує, вона переноситься в новий кластер.
    /// </summary>
    Task AddToUnknownClusterAsync(Guid participantId, Guid unknownClusterId, string? reason, CancellationToken ct);

    /// <summary>
    /// Позначає raw-учасника як канонічного актора.
    /// Якщо кластера ще нема, він створюється автоматично.
    /// </summary>
    Task ResolveAsActorAsync(Guid participantId, string displayName, string? role, string? callsign, string? note, CancellationToken ct);
}
