//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Analytics.Abstractions;

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
