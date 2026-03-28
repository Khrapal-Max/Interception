//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Models.PatternRecognition;

namespace Interception.UI.Application.Interceptions.Abstractions.Candidates;

/// <summary>
/// Будує аналітичну карту зв'язків між особами.
/// </summary>
public interface ILinkMapService
{
    /// <summary>
    /// Будує карту зв'язків за вказаний період.
    /// </summary>
    Task<LinkMapModel> BuildAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default);
}
