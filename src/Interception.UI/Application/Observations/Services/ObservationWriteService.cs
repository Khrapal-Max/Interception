//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Application.Observations.Dtos.Import;
using Interception.UI.Application.Observations.Import;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Observations.Services;

/// <summary>
/// EF-backed write service for observation aggregate.
/// </summary>
public sealed partial class ObservationWriteService(IDbContextFactory<AppDbContext> dbFactory) : IObservationWriteService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<ObservationSaveResultDto> CreateAsync(ObservationUpsertRequestDto request, CancellationToken ct)
    {
        ValidateRequest(request);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var observation = BuildNewObservation(request);

        var duplicateId = await db.Observations
            .AsNoTracking()
            .Where(x => x.ContentHash == observation.ContentHash)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (duplicateId is not null)
            return new ObservationSaveResultDto(duplicateId.Value, true, observation.ContentHash);

        db.Observations.Add(observation);
        await db.SaveChangesAsync(ct);
        return new ObservationSaveResultDto(observation.Id, false, observation.ContentHash);
    }

    public async Task<ObservationSaveResultDto> UpdateAsync(Guid observationId, ObservationUpsertRequestDto request, CancellationToken ct)
    {
        if (observationId == Guid.Empty)
            throw new ArgumentException("Observation id is required.", nameof(observationId));

        ValidateRequest(request);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var observation = await db.Observations
            .Include(x => x.Participants)
            .Include(x => x.Tags)
            .Include(x => x.ProbableActions)
            .FirstOrDefaultAsync(x => x.Id == observationId, ct)
            ?? throw new InvalidOperationException("Observation was not found.");

        observation.UpdateTiming(request.ObservedDate);
        observation.UpdateContext(
            request.ActionRaw,
            request.Layer,
            request.RmRaw,
            request.PointRaw,
            request.LocationRaw,
            request.DistrictRaw,
            request.Note);
        observation.UpdateSubdivision(request.SubdivisionRaw, request.SubdivisionStrength, request.SubdivisionSource);

        if (request.ObservationActionId is null)
            observation.ClearBoundAction();
        else
            observation.BindAction(request.ObservationActionId.Value);

        SyncParticipants(observation, request.Participants);
        SyncTags(observation, request.Tags);
        SyncProbableActions(observation, request.ProbableActions);

        var duplicateId = await db.Observations
            .AsNoTracking()
            .Where(x => x.Id != observationId && x.ContentHash == observation.ContentHash)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (duplicateId is not null)
            return new ObservationSaveResultDto(duplicateId.Value, true, observation.ContentHash);

        await db.SaveChangesAsync(ct);
        return new ObservationSaveResultDto(observation.Id, false, observation.ContentHash);
    }

    private static Observation BuildNewObservation(ObservationUpsertRequestDto request)
    {
        var observation = Observation.Create(
            request.ObservedDate,
            request.ActionRaw,
            request.Layer,
            request.RmRaw,
            request.PointRaw,
            request.LocationRaw,
            request.DistrictRaw,
            request.SubdivisionRaw,
            request.SubdivisionStrength,
            request.SubdivisionSource,
            request.Note);

        if (request.ObservationActionId is not null)
            observation.BindAction(request.ObservationActionId.Value);

        foreach (var participant in NormalizeParticipants(request.Participants))
            observation.AddParticipant(participant.LabelRaw, participant.IsUnknown, participant.RoleRaw, participant.Ordinal);

        foreach (var tag in NormalizeTags(request.Tags))
            observation.AddTag(tag.RawValue, tag.Kind, tag.Source, tag.TagCatalogId);

        foreach (var probableAction in NormalizeProbableActions(request.ProbableActions))
        {
            observation.AddProbableAction(
                probableAction.ObservationActionId,
                probableAction.Confidence,
                probableAction.Reason,
                probableAction.Source);
        }

        return observation;
    }

    private static void SyncParticipants(Observation observation, IReadOnlyList<ObservationParticipantUpsertDto> requestParticipants)
    {
        var normalized = NormalizeParticipants(requestParticipants);
        var incomingIds = normalized.Where(x => x.Id is not null).Select(x => x.Id!.Value).ToHashSet();

        foreach (var participant in observation.Participants.Where(x => !incomingIds.Contains(x.Id)).ToList())
            observation.RemoveParticipant(participant.Id);

        foreach (var participant in normalized)
        {
            if (participant.Id is null)
            {
                observation.AddParticipant(participant.LabelRaw, participant.IsUnknown, participant.RoleRaw, participant.Ordinal);
                continue;
            }

            observation.UpdateParticipant(participant.Id.Value, participant.LabelRaw, participant.IsUnknown, participant.RoleRaw);
        }
    }

    private static void SyncTags(Observation observation, IReadOnlyList<ObservationTagUpsertDto> requestTags)
    {
        var normalized = NormalizeTags(requestTags);
        var incomingIds = normalized.Where(x => x.Id is not null).Select(x => x.Id!.Value).ToHashSet();

        foreach (var tag in observation.Tags.Where(x => !incomingIds.Contains(x.Id)).ToList())
            observation.RemoveTag(tag.Id);

        foreach (var tag in normalized)
        {
            if (tag.Id is null)
            {
                observation.AddTag(tag.RawValue, tag.Kind, tag.Source, tag.TagCatalogId);
                continue;
            }

            observation.UpdateTag(tag.Id.Value, tag.RawValue, tag.Kind, tag.TagCatalogId);
        }
    }

    private static void SyncProbableActions(Observation observation, IReadOnlyList<ObservationProbableActionUpsertDto> requestProbableActions)
    {
        var normalized = NormalizeProbableActions(requestProbableActions);
        var incomingIds = normalized.Where(x => x.Id is not null).Select(x => x.Id!.Value).ToHashSet();

        foreach (var probableAction in observation.ProbableActions.Where(x => !incomingIds.Contains(x.Id)).ToList())
            observation.RemoveProbableAction(probableAction.Id);

        foreach (var probableAction in normalized)
        {
            if (probableAction.Id is null)
            {
                observation.AddProbableAction(
                    probableAction.ObservationActionId,
                    probableAction.Confidence,
                    probableAction.Reason,
                    probableAction.Source);
                continue;
            }

            observation.UpdateProbableAction(probableAction.Id.Value, probableAction.Confidence, probableAction.Reason);
        }
    }

    private static List<NormalizedParticipantWrite> NormalizeParticipants(IReadOnlyList<ObservationParticipantUpsertDto> requestParticipants)
    {
        var result = new List<NormalizedParticipantWrite>();

        foreach (var raw in requestParticipants.OrderBy(x => x.Ordinal ?? int.MaxValue))
        {
            var labelRaw = string.IsNullOrWhiteSpace(raw.LabelRaw) ? null : raw.LabelRaw.Trim();
            var roleRaw = string.IsNullOrWhiteSpace(raw.RoleRaw) ? null : raw.RoleRaw.Trim();
            var isUnknown = raw.IsUnknown || UnknownIdentityText.IsUnknownLabel(labelRaw);

            if (labelRaw is null && roleRaw is null && !isUnknown)
                continue;

            result.Add(new NormalizedParticipantWrite(
                raw.Id,
                UnknownIdentityText.NormalizeRawUnknownLabel(labelRaw),
                isUnknown,
                roleRaw,
                raw.Ordinal));
        }

        return result;
    }

    private static IReadOnlyList<ObservationTagUpsertDto> NormalizeTags(IReadOnlyList<ObservationTagUpsertDto> requestTags)
    {
        return [.. requestTags
            .Where(x => !string.IsNullOrWhiteSpace(x.RawValue))
            .Select(x => new ObservationTagUpsertDto
            {
                Id = x.Id,
                RawValue = x.RawValue.Trim(),
                Kind = x.Kind,
                Source = x.Source,
                TagCatalogId = x.TagCatalogId
            })];
    }

    private static IReadOnlyList<ObservationProbableActionUpsertDto> NormalizeProbableActions(IReadOnlyList<ObservationProbableActionUpsertDto> requestProbableActions)
    {
        return [.. requestProbableActions
            .Where(x => x.ObservationActionId != Guid.Empty)
            .Select(x => new ObservationProbableActionUpsertDto
            {
                Id = x.Id,
                ObservationActionId = x.ObservationActionId,
                Confidence = x.Confidence,
                Reason = string.IsNullOrWhiteSpace(x.Reason) ? null : x.Reason.Trim(),
                Source = x.Source
            })];
    }

    private static void ValidateRequest(ObservationUpsertRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ActionRaw))
            throw new ArgumentException("Action is required.", nameof(request));
    }
}
