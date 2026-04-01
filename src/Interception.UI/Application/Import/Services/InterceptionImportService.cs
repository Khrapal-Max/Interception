//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Import.Abstractions;
using Interception.UI.Application.Import.Dtos;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Import.Services;

public sealed class InterceptionImportService(
    IDbContextFactory<AppDbContext> dbFactory) : IInterceptionImportService
{
    public async Task<ImportResultDto> ImportAsync(
        Stream stream,
        string operatorName,
        CancellationToken cancellationToken = default)
    {
        // ClosedXML читає синхронно — копіюємо в MemoryStream асинхронно
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        var parser = new ExcelImportParser();
        var parsed = parser.Parse(memoryStream);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var actions = await db.InterceptionActions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // FIX: Attach кожну дію один раз щоб EF не намагався їх вставити повторно.
        // Без Attach — при db.InterceptionMessages.Add(message) EF бачить
        // навігаційну властивість message.InterceptionAction і додає її в ChangeTracker
        // як Added, що призводить до duplicate key (23505) при SaveChangesAsync.
        foreach (var action in actions)
            db.InterceptionActions.Attach(action);

        var cache = new ImportContextCache(actions);
        var errors = new List<ImportRowErrorDto>();
        var imported = 0;

        foreach (var (row, parseError) in parsed)
        {
            if (parseError is not null) { errors.Add(parseError); continue; }

            var (Message, Error) = ProcessRow(row!, cache, operatorName);

            if (Error is not null) { errors.Add(Error); continue; }

            db.InterceptionMessages.Add(Message!);
            imported++;
        }

        if (imported > 0)
            await db.SaveChangesAsync(cancellationToken);

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
        var action = cache.FindAction(row.ActionName);
        if (action is null)
            return (null, new ImportRowErrorDto(
                row.RowNumber,
                $"Дію '{row.ActionName}' не знайдено в довіднику. Рядок пропущено."));

        var rawDate = row.Date.ToDateTime(row.Time, DateTimeKind.Local);
        var observedDate = ConverterDateTimeExtensions.ToUtc(rawDate);

        InterceptionMessage message;
        try
        {
            message = InterceptionMessage.Create(
                observedDate: observedDate,
                frequency: row.Frequency,
                division: row.Division,
                vectorSignal: row.VectorSignal,
                locationClass: LocationClass.NoInfo,
                interceptionAction: action,
                note: row.Details,
                createdBy: operatorName,
                pointSignal: row.PointSignal);
        }
        catch (Exception ex)
        {
            return (null, new ImportRowErrorDto(row.RowNumber, ex.Message));
        }

        var initiatorRole = cache.ResolveParticipantRole(row.InitiatorName, row.InitiatorRole);
        message.AddParticipant(row.InitiatorName, row.InitiatorName is null, initiatorRole, ordinal: 1);

        var responderRole = cache.ResolveParticipantRole(row.ResponderName, row.ResponderRole);
        var responderIsUnknown = row.ResponderName is null;

        if (!string.Equals(row.InitiatorName, row.ResponderName, StringComparison.OrdinalIgnoreCase)
            || row.InitiatorName is null || responderIsUnknown)
        {
            message.AddParticipant(row.ResponderName, responderIsUnknown, responderRole, ordinal: 2);
        }

        return (message, null);
    }
}
