//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Common.Events;
using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Interceptions.Events;
using Interception.UI.Application.Registry.Support;
using Interception.UI.Domain.Interceptions;
using Interception.UI.Domain.ValueObjects;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services;

/// <summary>
/// Реалізація write-side сценаріїв для повідомлень перехоплення.
/// </summary>
public sealed class InterceptionCommandService(
    IDbContextFactory<AppDbContext> dbFactory,
    IIntegrationEventPublisher? eventPublisher = null) : IInterceptionCommandService
{
    /// <inheritdoc />
    public async Task<Guid> CreateAsync(
        InterceptionFormDto form,
        string operatorName,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var action = await db.InterceptionActions.FindAsync([form.InterceptionActionId], ct)
            ?? throw new InvalidOperationException($"InterceptionAction '{form.InterceptionActionId}' не знайдено.");

        var roleMap = await ParticipantRoleCatalogSupport.LoadRoleMapAsync(db, ct);

        var message = InterceptionMessage.Create(
            form.ObservedDate,
            FrequencyCode.Create(form.Frequency)?.Value,
            DivisionName.Create(form.Division)?.Value,
            form.VectorSignal,
            action,
            form.Note,
            operatorName,
            form.PointSignal);

        foreach (var p in form.Participants.OrderBy(p => p.Ordinal))
            message.AddParticipant(
                PersonName.Create(p.Name)?.Value,
                p.IsUnknown,
                RoleName.Create(ParticipantRoleCatalogSupport.NormalizeRole(p.Role, roleMap))?.Value,
                p.Ordinal);

        foreach (var label in form.Labels)
            message.AddLabel(label);

        await TopologySnapshotStateMarker.MarkAllCompletedSnapshotsAsStaleAsync(db, ct);
        db.InterceptionMessages.Add(message);
        await db.SaveChangesAsync(ct);
        if (eventPublisher is not null)
        {
            await eventPublisher.PublishAsync(new InterceptionChangedIntegrationEvent(
                message.Id,
                InterceptionChangeType.Created,
                DateTime.UtcNow), ct);
        }
        return message.Id;
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

        var roleMap = await ParticipantRoleCatalogSupport.LoadRoleMapAsync(db, ct);

        message.Update(form.ObservedDate, FrequencyCode.Create(form.Frequency)?.Value, DivisionName.Create(form.Division)?.Value, form.VectorSignal, action, form.Note, form.PointSignal);

        foreach (var p in message.Participants.ToList())
            message.RemoveParticipant(p.Id);
        foreach (var p in form.Participants.OrderBy(p => p.Ordinal))
            message.AddParticipant(
                PersonName.Create(p.Name)?.Value,
                p.IsUnknown,
                RoleName.Create(ParticipantRoleCatalogSupport.NormalizeRole(p.Role, roleMap))?.Value,
                p.Ordinal);

        foreach (var l in message.Labels.ToList())
            message.RemoveLabel(l.Id);
        foreach (var label in form.Labels)
            message.AddLabel(label);

        await TopologySnapshotStateMarker.MarkAllCompletedSnapshotsAsStaleAsync(db, ct);
        await db.SaveChangesAsync(ct);
        if (eventPublisher is not null)
        {
            await eventPublisher.PublishAsync(new InterceptionChangedIntegrationEvent(
                id,
                InterceptionChangeType.Updated,
                DateTime.UtcNow), ct);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var message = await db.InterceptionMessages.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"InterceptionMessage '{id}' не знайдено.");
        await TopologySnapshotStateMarker.MarkAllCompletedSnapshotsAsStaleAsync(db, ct);
        db.InterceptionMessages.Remove(message);
        await db.SaveChangesAsync(ct);
        if (eventPublisher is not null)
        {
            await eventPublisher.PublishAsync(new InterceptionChangedIntegrationEvent(
                id,
                InterceptionChangeType.Deleted,
                DateTime.UtcNow), ct);
        }
    }

}
