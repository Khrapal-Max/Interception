//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Models.Candidates;

namespace Interception.UI.Application.Interceptions.Abstractions.Candidates;

/// <summary>
/// Сервіс карти зв'язків.
/// </summary>
public interface ILinkMapService
{
    /// <summary>
    /// Будує карту зв'язків для вибраного періоду.
    /// </summary>
    Task<LinkMapModel> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default);
}
