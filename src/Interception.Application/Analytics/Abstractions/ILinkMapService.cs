//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Analytics.Dtos;

namespace Interception.Application.Analytics.Abstractions;

/// <summary>
/// Сервіс карти зв'язків.
/// </summary>
public interface ILinkMapService
{
    /// <summary>
    /// Будує карту зв'язків для вибраного періоду.
    /// </summary>
    Task<LinkMapDto> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default);
}
