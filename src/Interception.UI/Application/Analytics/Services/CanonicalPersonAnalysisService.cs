//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Analytics.Services;

/// <summary>
/// Сервіс пошуку та створення канонічних осіб для проблемних дубльованих випадків.
/// </summary>
public sealed class CanonicalPersonAnalysisService(
    IDbContextFactory<AppDbContext> dbFactory) : ICanonicalPersonAnalysisService
{
    public async Task<IReadOnlyList<CanonicalPersonCandidateDto>> GetCandidatesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var resolved = await db.ResolvedParticipants
            .AsNoTracking()
            .ToListAsync(ct);

        var observationContexts = await LoadObservationContextsAsync(db, ct);
        var canonicalMap = await LoadCanonicalMapAsync(db, ct);

        var candidates = resolved
            .GroupBy(x => NormalizeCandidateKey(x.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(group =>
            {
                var contexts = observationContexts.TryGetValue(group.Key!, out var value)
                    ? value
                    : ObservationContextSummary.Empty;

                var canonical = group
                    .Select(x => canonicalMap.TryGetValue(x.Id, out var item) ? item : null)
                    .FirstOrDefault(x => x is not null);

                return new CanonicalPersonCandidateDto
                {
                    CandidateKey = group.Key!,
                    DisplayName = group.Select(x => x.Name.Trim()).First(),
                    ConfirmedRowsCount = group.Count(),
                    DistinctFrequencyCount = contexts.Frequencies.Count,
                    DistinctDivisionCount = contexts.Divisions.Count,
                    HasCanonicalPerson = canonical is not null,
                    CanonicalPersonId = canonical?.Id,
                    LastConfirmedAtUtc = group.Max(x => x.ConfirmedAt)
                };
            })
            .Where(x => x.ConfirmedRowsCount > 1 || x.DistinctFrequencyCount > 1 || x.DistinctDivisionCount > 1 || x.HasCanonicalPerson)
            .OrderByDescending(x => x.HasCanonicalPerson)
            .ThenByDescending(x => x.ConfirmedRowsCount)
            .ThenByDescending(x => x.DistinctFrequencyCount)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return candidates;
    }

    public async Task<CanonicalPersonCandidateDetailsDto?> GetCandidateDetailsAsync(string candidateKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(candidateKey))
            return null;

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var resolved = (await db.ResolvedParticipants
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Division)
            .ThenBy(x => x.ConfirmedAt)
            .ToListAsync(ct))
            .Where(x => NormalizeCandidateKey(x.Name) == candidateKey)
            .ToList();

        if (resolved.Count == 0)
            return null;

        var contexts = await LoadObservationContextsAsync(db, ct);
        var canonicalMap = await LoadCanonicalMapAsync(db, ct);

        var linkedCanonicalIds = resolved
            .Where(x => canonicalMap.ContainsKey(x.Id))
            .Select(x => canonicalMap[x.Id]!.Id)
            .Distinct()
            .ToList();

        var primaryCanonical = resolved
            .Select(x => canonicalMap.TryGetValue(x.Id, out var item) ? item : null)
            .FirstOrDefault(x => x is not null);

        var context = contexts.TryGetValue(candidateKey, out var value)
            ? value
            : ObservationContextSummary.Empty;

        return new CanonicalPersonCandidateDetailsDto
        {
            CandidateKey = candidateKey,
            DisplayName = resolved.Select(x => x.Name.Trim()).First(),
            HasCanonicalPerson = primaryCanonical is not null,
            CanonicalPersonId = primaryCanonical?.Id,
            CanonicalDisplayName = primaryCanonical?.DisplayName,
            CanonicalNote = primaryCanonical?.Note,
            Warning = linkedCanonicalIds.Count > 1
                ? "Для цього імені вже існує більше однієї канонічної особи. Потрібна ручна перевірка."
                : null,
            Frequencies = [.. context.Frequencies],
            Divisions = [.. context.Divisions],
            Rows = [.. resolved.Select(x => new CanonicalPersonCandidateRowDto
            {
                ResolvedParticipantId = x.Id,
                Name = x.Name,
                Role = x.Role,
                Division = x.Division,
                ConfirmedAtUtc = x.ConfirmedAt,
                IsLinkedToCanonical = canonicalMap.ContainsKey(x.Id),
                ObservationCount = context.ObservationCount,
                Frequencies = [.. context.Frequencies],
                Divisions = [.. context.Divisions]
            })]
        };
    }

    public async Task<CanonicalPersonCandidateDetailsDto> CreateCanonicalAsync(
        string candidateKey,
        IReadOnlyCollection<Guid> resolvedParticipantIds,
        string? note,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(candidateKey))
            throw new ArgumentException("Ключ кандидата обов'язковий.", nameof(candidateKey));

        if (resolvedParticipantIds is null || resolvedParticipantIds.Count == 0)
            throw new InvalidOperationException("Оберіть хоча б один підтверджений рядок для створення канонічної особи.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var selected = await db.ResolvedParticipants
            .Where(x => resolvedParticipantIds.Contains(x.Id))
            .ToListAsync(ct);

        if (selected.Count != resolvedParticipantIds.Count)
            throw new InvalidOperationException("Частину підтверджених рядків не знайдено.");

        var alreadyLinked = await db.CanonicalPersonMembers
            .Where(x => resolvedParticipantIds.Contains(x.ResolvedParticipantId))
            .AnyAsync(ct);

        if (alreadyLinked)
            throw new InvalidOperationException("Один або кілька рядків уже входять до іншої канонічної особи.");

        var canonical = CanonicalPerson.Create(selected.Select(x => x.Name.Trim()).First(), note);
        foreach (var rowId in resolvedParticipantIds.Distinct())
            canonical.AddMember(rowId);

        db.CanonicalPersons.Add(canonical);
        await db.SaveChangesAsync(ct);

        return await GetCandidateDetailsAsync(candidateKey, ct)
            ?? throw new InvalidOperationException("Не вдалося перечитати створену канонічну особу.");
    }

    public async Task<CanonicalPersonCandidateDetailsDto> AttachToCanonicalAsync(
        Guid canonicalPersonId,
        IReadOnlyCollection<Guid> resolvedParticipantIds,
        string? note,
        CancellationToken ct = default)
    {
        if (resolvedParticipantIds is null || resolvedParticipantIds.Count == 0)
            throw new InvalidOperationException("Оберіть хоча б один підтверджений рядок для додавання.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var canonical = await db.CanonicalPersons
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == canonicalPersonId, ct)
            ?? throw new InvalidOperationException("Канонічну особу не знайдено.");

        var selected = await db.ResolvedParticipants
            .Where(x => resolvedParticipantIds.Contains(x.Id))
            .ToListAsync(ct);

        if (selected.Count != resolvedParticipantIds.Count)
            throw new InvalidOperationException("Частину підтверджених рядків не знайдено.");

        var foreignMembers = await db.CanonicalPersonMembers
            .Where(x => resolvedParticipantIds.Contains(x.ResolvedParticipantId) && x.CanonicalPersonId != canonicalPersonId)
            .AnyAsync(ct);

        if (foreignMembers)
            throw new InvalidOperationException("Один або кілька рядків уже входять до іншої канонічної особи.");

        foreach (var rowId in resolvedParticipantIds.Distinct())
        {
            if (canonical.Members.Any(x => x.ResolvedParticipantId == rowId))
                continue;

            canonical.AddMember(rowId);
        }

        if (!string.IsNullOrWhiteSpace(note))
            canonical.UpdateNote(note);

        await db.SaveChangesAsync(ct);

        var candidateKey = NormalizeCandidateKey(selected.Select(x => x.Name).First());
        return await GetCandidateDetailsAsync(candidateKey, ct)
            ?? throw new InvalidOperationException("Не вдалося перечитати оновлену канонічну особу.");
    }

    private static string NormalizeCandidateKey(string? name)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(name)?.Trim().ToUpperInvariant() ?? string.Empty;

    private static async Task<Dictionary<string, ObservationContextSummary>> LoadObservationContextsAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.InterceptionMessages
            .AsNoTracking()
            .SelectMany(
                message => message.Participants,
                (message, participant) => new ObservationContextRow(
                    participant.Name,
                    participant.IsUnknown,
                    message.Frequency,
                    message.Division))
            .Where(x => !x.IsUnknown && !string.IsNullOrWhiteSpace(x.Name))
            .ToListAsync(ct);

        return rows
            .GroupBy(x => NormalizeCandidateKey(x.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(
                x => x.Key,
                x => new ObservationContextSummary(
                    [.. x.Select(v => SemanticValueExtensions.NormalizeMeaningfulOrNull(v.Frequency))
                        .Where(v => v is not null)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)!],
                    [.. x.Select(v => SemanticValueExtensions.NormalizeMeaningfulOrNull(v.Division))
                        .Where(v => v is not null)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)!],
                    x.Count()));
    }

    private static async Task<Dictionary<Guid, CanonicalLookupItem>> LoadCanonicalMapAsync(AppDbContext db, CancellationToken ct)
    {
        return await db.CanonicalPersonMembers
            .AsNoTracking()
            .Include(x => x.CanonicalPerson)
            .ToDictionaryAsync(
                x => x.ResolvedParticipantId,
                x => new CanonicalLookupItem(
                    x.CanonicalPersonId,
                    x.CanonicalPerson.DisplayName,
                    x.CanonicalPerson.Note),
                ct);
    }

    private sealed record ObservationContextRow(string? Name, bool IsUnknown, string? Frequency, string? Division);

    private sealed record ObservationContextSummary(
        IReadOnlyList<string> Frequencies,
        IReadOnlyList<string> Divisions,
        int ObservationCount)
    {
        public static ObservationContextSummary Empty { get; } = new([], [], 0);
    }

    private sealed record CanonicalLookupItem(Guid Id, string DisplayName, string? Note);
}
