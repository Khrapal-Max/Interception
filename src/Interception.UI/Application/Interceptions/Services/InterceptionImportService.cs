//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Interceptions.Import;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services;

public sealed class InterceptionImportService(
    IDbContextFactory<AppDbContext> dbFactory) : IInterceptionImportService
{
    public async Task<ImportResult> ImportAsync(
        Stream stream,
        string operatorName,
        CancellationToken cancellationToken = default)
    {
        var parser = new ExcelImportParser();
        var parsed = parser.Parse(stream);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var actions = await db.InterceptionActions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var cache    = new ImportContextCache(actions);
        var errors   = new List<ImportRowError>();
        var imported = 0;

        foreach (var (row, parseError) in parsed)
        {
            if (parseError is not null) { errors.Add(parseError); continue; }

            var result = ProcessRow(row!, cache, operatorName);

            if (result.Error is not null) { errors.Add(result.Error); continue; }

            db.InterceptionMessages.Add(result.Message!);
            imported++;
        }

        if (imported > 0)
            await db.SaveChangesAsync(cancellationToken);

        return new ImportResult
        {
            ImportedCount = imported,
            SkippedCount  = errors.Count,
            Errors        = errors
        };
    }

    private static (InterceptionMessage? Message, ImportRowError? Error) ProcessRow(
        ImportRowDto row,
        ImportContextCache cache,
        string operatorName)
    {
        var frequency    = cache.ResolveFrequency(row.Frequency);
        var vectorSignal = cache.ResolveVectorSignal(row.VectorSignal);

        var action = cache.FindAction(row.ActionName);
        if (action is null)
            return (null, new ImportRowError(
                row.RowNumber,
                $"Дію '{row.ActionName}' не знайдено в довіднику. Рядок пропущено."));

        // ToDateTime з Kind=Local → DateTimeConverter.ToUtc конвертує в UTC
        // щоб Npgsql прийняв для 'timestamp with time zone'
        var rawDate      = row.Date.ToDateTime(row.Time, DateTimeKind.Local);
        var observedDate = DateTimeConverter.ToUtc(rawDate);

        InterceptionMessage message;
        try
        {
            message = InterceptionMessage.Create(
                observedDate      : observedDate,
                frequency         : frequency,
                division          : row.Division,
                vectorSignal      : vectorSignal,
                interceptionAction: action,
                note              : row.Details,
                createdBy         : operatorName,
                pointSignal       : row.PointSignal);
        }
        catch (Exception ex)
        {
            return (null, new ImportRowError(row.RowNumber, ex.Message));
        }

        var initiatorRole      = cache.ResolveParticipantRole(row.InitiatorName, row.InitiatorRole);
        message.AddParticipant(row.InitiatorName, row.InitiatorName is null, initiatorRole, ordinal: 1);

        var responderRole      = cache.ResolveParticipantRole(row.ResponderName, row.ResponderRole);
        var responderIsUnknown = row.ResponderName is null;

        if (!string.Equals(row.InitiatorName, row.ResponderName, StringComparison.OrdinalIgnoreCase)
            || row.InitiatorName is null || responderIsUnknown)
        {
            message.AddParticipant(row.ResponderName, responderIsUnknown, responderRole, ordinal: 2);
        }

        return (message, null);
    }
}
