//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Hypotheses.Dtos;

namespace Interception.UI.Application.Hypotheses.Abstractions;

/// <summary>
/// Application service for analytical person hypotheses built on top of raw observation participants.
/// </summary>
public interface IActorHypothesisService
{
    Task<ActorHypothesisRegistryPageDto> SearchAsync(ActorHypothesisFilterDto filter, CancellationToken ct);

    Task<ActorHypothesisDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<HypothesisSaveResultDto> CreateAsync(ActorHypothesisCreateDto request, string? createdBy, CancellationToken ct);

    Task UpdateAsync(Guid id, ActorHypothesisUpdateDto request, CancellationToken ct);

    Task AddParticipantAsync(Guid clusterId, Guid observationParticipantId, string? note, CancellationToken ct);

    Task RemoveParticipantAsync(Guid clusterId, Guid observationParticipantId, CancellationToken ct);

    Task MoveParticipantAsync(Guid sourceClusterId, Guid observationParticipantId, Guid targetClusterId, CancellationToken ct);

    Task MergeAsync(Guid sourceClusterId, Guid targetClusterId, CancellationToken ct);

    Task ResolveAsync(Guid clusterId, ResolvedActorUpsertDto request, string? createdBy, CancellationToken ct);

    Task ReopenAsync(Guid clusterId, CancellationToken ct);

    Task ArchiveAsync(Guid clusterId, CancellationToken ct);

    Task<IReadOnlyList<UnknownClusterLookupDto>> SearchOpenClustersAsync(string? query, int take, CancellationToken ct);
}
