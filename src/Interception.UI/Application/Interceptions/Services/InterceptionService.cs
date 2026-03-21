//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services;

/// <summary>
/// Основний сервіс для роботи UI з перехопленнями.
///
/// Використовує IDbContextFactory — кожен метод отримує власний
/// короткочасний DbContext і закриває його після завершення.
/// Це безпечно для Blazor Server де компоненти живуть довго.
/// </summary>
public sealed class InterceptionService(
    IDbContextFactory<AppDbContext> dbFactory) : IInterceptionService
{
    // -------------------------------------------------------------------------
    // Suggestions
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<string>> GetFrequencySuggestionsAsync(
        string? query = null,
        int take = 10,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var q = db.InterceptionMessages
            .Where(m => m.Frequency != null);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(m => m.Frequency!.StartsWith(query));

        return await q
            .GroupBy(m => m.Frequency!)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetVectorSignalSuggestionsAsync(
        string? query = null,
        int take = 10,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var q = db.InterceptionMessages
            .Where(m => m.VectorSignal != null);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(m => m.VectorSignal!.Contains(query));

        return await q
            .GroupBy(m => m.VectorSignal!)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ParticipantSuggestionDto>> GetParticipantSuggestionsAsync(
        string? query = null,
        int take = 15,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var q = db.InterceptionMessageParticipants
            .Where(p => !p.IsUnknown && p.Name != null);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Name!.StartsWith(query));

        return await q
            .GroupBy(p => p.Name!)
            .OrderByDescending(g => g.Count())
            .Take(take)
            .Select(g => new ParticipantSuggestionDto
            {
                Name = g.Key,
                Role = g
                    .Where(p => p.Role != null)
                    .OrderByDescending(p => p.InterceptionMessage.ObservedDate)
                    .Select(p => p.Role)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    // -------------------------------------------------------------------------
    // CRUD
    // -------------------------------------------------------------------------

    public async Task<PagedResult<InterceptionListItemDto>> GetPagedAsync(
        InterceptionFilter filter,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var q = db.InterceptionMessages
            .Include(m => m.InterceptionAction)
            .Include(m => m.Participants)
            .Include(m => m.Labels)
            .AsNoTracking()
            .AsQueryable();

        if (filter.DateFrom.HasValue)
            q = q.Where(m => m.ObservedDate >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            q = q.Where(m => m.ObservedDate <= filter.DateTo.Value);

        if (!string.IsNullOrWhiteSpace(filter.Frequency))
            q = q.Where(m => m.Frequency == filter.Frequency);

        if (!string.IsNullOrWhiteSpace(filter.ParticipantName))
            q = q.Where(m => m.Participants
                .Any(p => p.Name != null && p.Name.Contains(filter.ParticipantName)));

        if (!string.IsNullOrWhiteSpace(filter.LabelName))
            q = q.Where(m => m.Labels
                .Any(l => l.NameLabel == filter.LabelName));

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(m => m.ObservedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new InterceptionListItemDto
            {
                Id           = m.Id,
                ObservedDate = m.ObservedDate,
                Frequency    = m.Frequency,
                VectorSignal = m.VectorSignal,
                Division     = m.Division,
                ActionName   = m.InterceptionAction != null ? m.InterceptionAction.Name : null,
                Participants = m.Participants
                    .OrderBy(p => p.Ordinal)
                    .Select(p => new ParticipantBriefDto
                    {
                        Name      = p.Name,
                        Role      = p.Role,
                        IsUnknown = p.IsUnknown,
                        Ordinal   = p.Ordinal
                    })
                    .ToList(),
                Labels = m.Labels.Select(l => l.NameLabel).ToList()
            })
            .ToListAsync(ct);

        return new PagedResult<InterceptionListItemDto>(items, total, page, pageSize);
    }

    public async Task<InterceptionMessage?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        return await db.InterceptionMessages
            .Include(m => m.InterceptionAction)
            .Include(m => m.Participants)
            .Include(m => m.Labels)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<InterceptionMessage> CreateAsync(
        InterceptionFormDto form,
        string operatorName,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var action = await db.InterceptionActions
            .FindAsync([form.InterceptionActionId], ct)
            ?? throw new InvalidOperationException(
                $"InterceptionAction '{form.InterceptionActionId}' не знайдено.");

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

    public async Task UpdateAsync(
        Guid id,
        InterceptionFormDto form,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var message = await db.InterceptionMessages
            .Include(m => m.Participants)
            .Include(m => m.Labels)
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new InvalidOperationException(
                $"InterceptionMessage '{id}' не знайдено.");

        var action = await db.InterceptionActions
            .FindAsync([form.InterceptionActionId], ct)
            ?? throw new InvalidOperationException(
                $"InterceptionAction '{form.InterceptionActionId}' не знайдено.");

        message.Update(
            form.ObservedDate, form.Frequency, form.Division,
            form.VectorSignal, action, form.Note, form.PointSignal);

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

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var message = await db.InterceptionMessages
            .FindAsync([id], ct)
            ?? throw new InvalidOperationException(
                $"InterceptionMessage '{id}' не знайдено.");

        db.InterceptionMessages.Remove(message);
        await db.SaveChangesAsync(ct);
    }
}
