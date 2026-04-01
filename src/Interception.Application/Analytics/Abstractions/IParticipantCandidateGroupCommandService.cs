//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Analytics.Abstractions;

/// <summary>
/// Write-side сервіс для підтвердження або відхилення груп кандидатів.
/// </summary>
public interface IParticipantCandidateGroupCommandService
{
    /// <summary>
    /// Підтверджує open-групу, транзакційно створюючи або оновлюючи <c>ResolvedParticipant</c>.
    /// </summary>
    Task ConfirmAsync(
        Guid groupId,
        string resolvedName,
        string resolvedBy,
        string? role = null,
        string? division = null,
        CancellationToken ct = default);

    /// <summary>
    /// Відхиляє open-групу.
    /// </summary>
    Task DismissAsync(Guid groupId, string resolvedBy, CancellationToken ct = default);
}
