//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Registry.Services;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain;
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

        var roleMap = await ParticipantRoleCatalogSupport.LoadRoleMapAsync(db, ct);

        var message = InterceptionMessage.Create(
            form.ObservedDate,
            form.Frequency,
            form.Division,
            form.VectorSignal,
            action,
            form.Note,
            operatorName,
            form.PointSignal);

        foreach (var p in form.Participants.OrderBy(p => p.Ordinal))
            message.AddParticipant(p.Name, p.IsUnknown, ParticipantRoleCatalogSupport.NormalizeRole(p.Role, roleMap), p.Ordinal);

        foreach (var label in form.Labels)
            message.AddLabel(label);

        db.InterceptionMessages.Add(message);
        await MarkCompletedTopologySnapshotsAsStaleAsync(db, ct);
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

        var roleMap = await ParticipantRoleCatalogSupport.LoadRoleMapAsync(db, ct);

        message.Update(form.ObservedDate, form.Frequency, form.Division, form.VectorSignal, action, form.Note, form.PointSignal);

        foreach (var p in message.Participants.ToList())
            message.RemoveParticipant(p.Id);
        foreach (var p in form.Participants.OrderBy(p => p.Ordinal))
            message.AddParticipant(p.Name, p.IsUnknown, ParticipantRoleCatalogSupport.NormalizeRole(p.Role, roleMap), p.Ordinal);

        foreach (var l in message.Labels.ToList())
            message.RemoveLabel(l.Id);
        foreach (var label in form.Labels)
            message.AddLabel(label);

        await MarkCompletedTopologySnapshotsAsStaleAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var message = await db.InterceptionMessages.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"InterceptionMessage '{id}' не знайдено.");
        db.InterceptionMessages.Remove(message);
        await MarkCompletedTopologySnapshotsAsStaleAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }


    private static async Task MarkCompletedTopologySnapshotsAsStaleAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var completedRuns = await db.TopologySnapshotRuns
            .Where(x => x.Status == Interception.UI.Domain.Enums.TopologySnapshotRunStatus.Completed && !x.IsStale)
            .ToListAsync(ct);

        foreach (var run in completedRuns)
            run.MarkStale();
    }

}
