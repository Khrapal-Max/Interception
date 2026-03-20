//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Hypotheses.Dtos;

namespace Interception.UI.Application.Hypotheses.Abstractions;

/// <summary>
/// Application service for analytical subdivision hypotheses built on top of weak or unknown subdivision hints.
/// </summary>
public interface ISubdivisionHypothesisService
{
    Task<SubdivisionHypothesisRegistryPageDto> SearchAsync(SubdivisionHypothesisFilterDto filter, CancellationToken ct);

    Task<SubdivisionHypothesisDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<HypothesisSaveResultDto> CreateAsync(SubdivisionHypothesisCreateDto request, string? createdBy, CancellationToken ct);

    Task UpdateAsync(Guid id, SubdivisionHypothesisUpdateDto request, CancellationToken ct);

    Task AddObservationAsync(Guid clusterId, Guid observationId, string? note, CancellationToken ct);

    Task RemoveObservationAsync(Guid clusterId, Guid observationId, CancellationToken ct);

    Task MoveObservationAsync(Guid sourceClusterId, Guid observationId, Guid targetClusterId, CancellationToken ct);

    Task MergeAsync(Guid sourceClusterId, Guid targetClusterId, CancellationToken ct);

    Task ResolveAsync(Guid clusterId, ResolvedSubdivisionUpsertDto request, string? createdBy, CancellationToken ct);

    Task ReopenAsync(Guid clusterId, CancellationToken ct);

    Task ArchiveAsync(Guid clusterId, CancellationToken ct);

    Task<IReadOnlyList<UnknownSubdivisionClusterLookupDto>> SearchOpenClustersAsync(string? query, int take, CancellationToken ct);
}
