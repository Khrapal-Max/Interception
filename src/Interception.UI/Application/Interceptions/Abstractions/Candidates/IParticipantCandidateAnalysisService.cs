//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Abstractions.Candidates;

/// <summary>
/// Сервіс аналізу невідомих учасників і побудови / збагачення груп кандидатів.
/// </summary>
public interface IParticipantCandidateAnalysisService
{
    /// <summary>
    /// Запускає аналіз і створює або збагачує open-групи.
    /// </summary>
    Task<int> RunAsync(CancellationToken ct = default);
}
