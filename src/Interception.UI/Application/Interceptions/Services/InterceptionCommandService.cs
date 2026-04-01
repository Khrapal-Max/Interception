//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services;

/// <summary>
/// Реалізація write-side сценаріїв для повідомлень перехоплення.
/// </summary>
public sealed class InterceptionCommandService(IDbContextFactory<AppDbContext> dbFactory) : IInterceptionCommandService
{
    /// <inheritdoc />
    public async Task<InterceptionMessage> CreateAsync(
        InterceptionFormDto form,
        string operatorName,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var action = await db.InterceptionActions.FindAsync([form.InterceptionActionId], ct)
            ?? throw new InvalidOperationException($"InterceptionAction '{form.InterceptionActionId}' не знайдено.");

        var locationClass = ResolveLocationClass(form);
        var message = InterceptionMessage.Create(
            form.ObservedDate,
            form.Frequency,
            form.Division,
            form.VectorSignal,
            form.LocationDetails,
            locationClass,
            action,
            form.Note,
            operatorName,
            form.PointSignal);

        foreach (var p in form.Participants.OrderBy(p => p.Ordinal))
            message.AddParticipant(p.Name, p.IsUnknown, p.Role, p.Ordinal);

        foreach (var label in form.Labels)
            message.AddLabel(label);

        db.InterceptionMessages.Add(message);
        await db.SaveChangesAsync(ct);
        return message;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Guid id, InterceptionFormDto form, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var message = await db.InterceptionMessages
            .Include(m => m.Participants)
            .Include(m => m.Labels)
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new InvalidOperationException($"InterceptionMessage '{id}' не знайдено.");

        var action = await db.InterceptionActions.FindAsync([form.InterceptionActionId], ct)
            ?? throw new InvalidOperationException($"InterceptionAction '{form.InterceptionActionId}' не знайдено.");

        var locationClass = ResolveLocationClass(form);
        message.Update(form.ObservedDate, form.Frequency, form.Division, form.VectorSignal, form.LocationDetails, locationClass, action, form.Note, form.PointSignal);

        foreach (var p in message.Participants.ToList())
            message.RemoveParticipant(p.Id);
        foreach (var p in form.Participants.OrderBy(p => p.Ordinal))
            message.AddParticipant(p.Name, p.IsUnknown, p.Role, p.Ordinal);

        foreach (var l in message.Labels.ToList())
            message.RemoveLabel(l.Id);
        foreach (var label in form.Labels)
            message.AddLabel(label);

        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var message = await db.InterceptionMessages.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"InterceptionMessage '{id}' не знайдено.");
        db.InterceptionMessages.Remove(message);
        await db.SaveChangesAsync(ct);
    }

    private static LocationClass ResolveLocationClass(InterceptionFormDto form)
    {
        if (form.LocationClass != LocationClass.NoInfo)
            return form.LocationClass;

        var locationDetails = form.LocationDetails?.Trim();
        if (string.IsNullOrWhiteSpace(locationDetails))
            return LocationClass.NoInfo;

        if (locationDetails.Contains("->", StringComparison.OrdinalIgnoreCase)
            || locationDetails.Contains("→", StringComparison.OrdinalIgnoreCase)
            || locationDetails.Contains('-', StringComparison.OrdinalIgnoreCase)
            || locationDetails.Contains("маршрут", StringComparison.OrdinalIgnoreCase)
            || locationDetails.Contains("напрям", StringComparison.OrdinalIgnoreCase)
            || locationDetails.Contains("курс", StringComparison.OrdinalIgnoreCase))
        {
            return LocationClass.Route;
        }

        if (locationDetails.Contains("район", StringComparison.OrdinalIgnoreCase)
            || locationDetails.Contains("сектор", StringComparison.OrdinalIgnoreCase)
            || locationDetails.Contains("зона", StringComparison.OrdinalIgnoreCase))
        {
            return LocationClass.Zone;
        }

        return LocationClass.Point;

    }
}
