//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Окремий аналітичний сервіс «зрізів» поверх карти зв'язків.
/// Дає агрегований вигляд груп без змішування з логікою побудови графа.
/// </summary>
public interface ILinkMapSliceService
{
    /// <summary>
    /// Будує зрізи по групах: особи, спільні частоти, спільні дії, прогалини даних.
    /// </summary>
    Task<LinkMapSliceDto> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default);
}
