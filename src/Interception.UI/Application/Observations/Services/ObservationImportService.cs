//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos.Import;
using Interception.UI.Application.Observations.Import;
using Interception.UI.Domain.Enums;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Observations.Services;

/// <summary>
/// Batch import of already parsed observation rows.
/// </summary>
public sealed partial class ObservationImportService(IDbContextFactory<AppDbContext> dbFactory) : IObservationImportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<ObservationImportResultDto> ImportAsync(
        IReadOnlyCollection<ObservationImportRowDto> rows,
        string source,
        Guid? sourceFileId,
        string? createdBy,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rows);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var importedCount = 0;
        var duplicateCount = 0;
        var errors = new List<ObservationImportErrorDto>();
        var batchHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows.OrderBy(x => x.RowNumber))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(row.ActionRaw))
                    throw new InvalidOperationException("Action is required.");

                var observation = Domain.Observation.Create(
                    row.ObservedDate,
                    row.ActionRaw,
                    row.Layer,
                    row.RmRaw,
                    row.PointRaw,
                    row.LocationRaw,
                    row.DistrictRaw,
                    row.SubdivisionRaw,
                    row.SubdivisionStrength,
                    row.SubdivisionSource ?? ObservationSubdivisionSource.Import,
                    row.Note,
                    source,
                    sourceFileId,
                    row.RowNumber,
                    createdBy);

                foreach (var participant in NormalizeParticipants(row.Participants))
                    observation.AddParticipant(participant.LabelRaw, participant.IsUnknown, participant.RoleRaw);

                if (!batchHashes.Add(observation.ContentHash))
                {
                    duplicateCount++;
                    continue;
                }

                var existsInDb = await db.Observations
                    .AsNoTracking()
                    .AnyAsync(x => x.ContentHash == observation.ContentHash, ct);

                if (existsInDb)
                {
                    duplicateCount++;
                    continue;
                }

                db.Observations.Add(observation);
                importedCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new ObservationImportErrorDto(row.RowNumber, ex.Message));
            }
        }

        if (importedCount > 0)
            await db.SaveChangesAsync(ct);

        return new ObservationImportResultDto(importedCount, duplicateCount, errors.Count, errors);
    }

    private static List<NormalizedParticipantImport> NormalizeParticipants(IReadOnlyList<ObservationImportParticipantDto> participants)
    {
        var result = new List<NormalizedParticipantImport>();

        foreach (var participant in participants)
        {
            var labelRaw = string.IsNullOrWhiteSpace(participant.LabelRaw) ? null : participant.LabelRaw.Trim();
            var roleRaw = string.IsNullOrWhiteSpace(participant.RoleRaw) ? null : participant.RoleRaw.Trim();
            var isUnknown = participant.IsUnknown || UnknownIdentityText.IsUnknownLabel(labelRaw);

            if (labelRaw is null && roleRaw is null && !isUnknown)
                continue;

            result.Add(new NormalizedParticipantImport(
                UnknownIdentityText.NormalizeRawUnknownLabel(labelRaw),
                isUnknown,
                roleRaw));
        }

        return result;
    }
}
