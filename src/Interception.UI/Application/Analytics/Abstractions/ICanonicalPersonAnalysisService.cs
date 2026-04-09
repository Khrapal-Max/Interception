//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Аналітичний сервіс пошуку та зведення підтверджених записів в один об’єднаний профіль.
/// </summary>
public interface ICanonicalPersonAnalysisService
{
    Task<IReadOnlyList<CanonicalPersonCandidateDto>> GetCandidatesAsync(CancellationToken ct = default);
    Task<CanonicalPersonCandidateDetailsDto?> GetCandidateDetailsAsync(string candidateKey, CancellationToken ct = default);

    Task<CanonicalPersonCandidateDetailsDto> CreateCanonicalAsync(
        string candidateKey,
        IReadOnlyCollection<Guid> resolvedParticipantIds,
        string displayName,
        string? note,
        CancellationToken ct = default);

    Task<CanonicalPersonCandidateDetailsDto> AttachToCanonicalAsync(
        Guid canonicalPersonId,
        IReadOnlyCollection<Guid> resolvedParticipantIds,
        string? note,
        CancellationToken ct = default);

    Task<CanonicalPersonCandidateDetailsDto> UpdateCanonicalAsync(
        string candidateKey,
        Guid canonicalPersonId,
        string displayName,
        string? note,
        CancellationToken ct = default);

    Task DeleteCanonicalAsync(Guid canonicalPersonId, CancellationToken ct = default);
}
