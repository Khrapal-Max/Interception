//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Common.Events;
using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Interceptions.Events;
using Interception.UI.Application.Registry.Support;
using Interception.UI.Domain.Entities;
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

        await EnsureParticipantsExistInRegistryAsync(db, form, roleMap, ct);

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

        await EnsureParticipantsExistInRegistryAsync(db, form, roleMap, ct);

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

    private static async Task EnsureParticipantsExistInRegistryAsync(
        AppDbContext db,
        InterceptionFormDto form,
        IReadOnlyDictionary<string, string> roleMap,
        CancellationToken ct)
    {
        var normalizedFrequency = FrequencyCode.Create(form.Frequency)?.Value;
        var normalizedDivision = DivisionName.Create(form.Division)?.Value;

        var participantData = form.Participants
            .Where(p => !p.IsUnknown && !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new
            {
                Name = PersonName.Create(p.Name)?.Value,
                Role = RoleName.Create(ParticipantRoleCatalogSupport.NormalizeRole(p.Role, roleMap))?.Value
            })
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .ToList();

        if (participantData.Count == 0)
            return;

        var names = participantData
            .Select(x => x.Name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = await db.ResolvedParticipants
            .Where(x => names.Contains(x.Name))
            .ToListAsync(ct);

        var knownContexts = candidates
            .Select(x => BuildContextKey(x.Name, ReadFrequency(db, x), x.Division))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var participant in participantData)
        {
            var contextKey = BuildContextKey(participant.Name!, normalizedFrequency, normalizedDivision);
            if (knownContexts.Contains(contextKey))
                continue;

            var resolved = ResolvedParticipant.Create(
                participant.Name!,
                confirmedBy: "auto-observation",
                role: participant.Role,
                division: normalizedDivision,
                frequency: normalizedFrequency);

            db.ResolvedParticipants.Add(resolved);
            knownContexts.Add(contextKey);
        }
    }

    private static string? ReadFrequency(AppDbContext db, ResolvedParticipant resolved)
        => db.Entry(resolved).Property<string?>("Frequency").CurrentValue;

    private static string BuildContextKey(string? name, string? frequency, string? division)
        => string.Join('|',
            NormalizeOptional(name),
            NormalizeOptional(frequency),
            NormalizeOptional(division));

    private static string NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

}
