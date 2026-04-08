//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Аналітичний сервіс пошуку кандидатів на канонічну особу.
/// </summary>
public interface ICanonicalPersonAnalysisService
{
    Task<IReadOnlyList<CanonicalPersonCandidateDto>> GetCandidatesAsync(CancellationToken ct = default);
    Task<CanonicalPersonCandidateDetailsDto?> GetCandidateDetailsAsync(string candidateKey, CancellationToken ct = default);
    Task<CanonicalPersonCandidateDetailsDto> CreateCanonicalAsync(
        string candidateKey,
        IReadOnlyCollection<Guid> resolvedParticipantIds,
        string? note,
        CancellationToken ct = default);
    Task<CanonicalPersonCandidateDetailsDto> AttachToCanonicalAsync(
        Guid canonicalPersonId,
        IReadOnlyCollection<Guid> resolvedParticipantIds,
        string? note,
        CancellationToken ct = default);
}
