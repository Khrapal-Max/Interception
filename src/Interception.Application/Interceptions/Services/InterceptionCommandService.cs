//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Abstractions;
using Interception.Application.Interceptions.Abstractions;
using Interception.Application.Interceptions.Dtos;
using Interception.Domain.Entities;

namespace Interception.Application.Interceptions.Services;

/// <summary>
/// Реалізація write-side сценаріїв для повідомлень перехоплення.
/// </summary>
public sealed class InterceptionCommandService(IAppDbContextFactory dbFactory) : IInterceptionCommandService
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

        message.Update(form.ObservedDate, form.Frequency, form.Division, form.VectorSignal, action, form.Note, form.PointSignal);

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
}
