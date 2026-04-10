//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Import.Abstractions;
using Interception.UI.Application.Import.Dtos;
using Interception.UI.Application.Registry.Services;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Import.Services;

public sealed class InterceptionImportService(
    IDbContextFactory<AppDbContext> dbFactory) : IInterceptionImportService
{
    public async Task<ImportResultDto> ImportAsync(
        Stream excelStream,
        string operatorName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(excelStream);

        await using var bufferedStream = new MemoryStream();
        await excelStream.CopyToAsync(bufferedStream, ct);
        bufferedStream.Position = 0;

        var parser = new ExcelImportParser();
        var parsed = parser.Parse(bufferedStream);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var actions = await db.InterceptionActions
            .ToListAsync(ct);

        var roleMap = await ParticipantRoleCatalogSupport.LoadRoleMapAsync(db, ct);

        var cache = new ImportContextCache(actions, roleMap);
        var errors = new List<ImportRowErrorDto>();
        var imported = 0;

        foreach (var item in parsed)
        {
            if (item.Error is not null)
            {
                errors.Add(item.Error);
                continue;
            }

            var row = item.Row!;
            var (message, error) = ProcessRow(row, cache, operatorName);

            if (error is not null)
            {
                errors.Add(error);
                continue;
            }

            db.InterceptionMessages.Add(message!);
            imported++;
        }

        if (imported > 0)
        {
            await MarkCompletedTopologySnapshotsAsStaleAsync(db, ct);
            await db.SaveChangesAsync(ct);
        }

        return new ImportResultDto
        {
            ImportedCount = imported,
            SkippedCount = errors.Count,
            Errors = errors
        };
    }

    private static (InterceptionMessage? Message, ImportRowErrorDto? Error) ProcessRow(
        ImportRowDto row,
        ImportContextCache cache,
        string operatorName)
    {
        return ImportRowToMessageMapper.TryMap(row, cache, operatorName);
    }

    private static async Task MarkCompletedTopologySnapshotsAsStaleAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var completedRuns = await db.TopologySnapshotRuns
            .Where(x => x.Status == TopologySnapshotRunStatus.Completed && !x.IsStale)
            .ToListAsync(ct);

        foreach (var run in completedRuns)
            run.MarkStale();
    }
}
