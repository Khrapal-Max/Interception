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
/// Сервіс пошуку та створення основних осіб для проблемних дубльованих випадків.
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
                var contexts = observationContexts.ByCandidateKey.TryGetValue(group.Key!, out var value)
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

        var resolvedRows = (await db.ResolvedParticipants
            .AsNoTracking()
            .Select(x => new ResolvedRow(
                x.Id,
                x.Name,
                EF.Property<string?>(x, "Frequency"),
                x.Role,
                x.Division,
                x.ConfirmedAt))
            .ToListAsync(ct))
            .Where(x => NormalizeCandidateKey(x.Name) == candidateKey)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Frequency, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Division, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ConfirmedAtUtc)
            .ToList();

        if (resolvedRows.Count == 0)
            return null;

        var contexts = await LoadObservationContextsAsync(db, ct);
        var canonicalMap = await LoadCanonicalMapAsync(db, ct);

        var linkedCanonicalIds = resolvedRows
            .Where(x => canonicalMap.ContainsKey(x.Id))
            .Select(x => canonicalMap[x.Id]!.Id)
            .Distinct()
            .ToList();

        var primaryCanonical = resolvedRows
            .Select(x => canonicalMap.TryGetValue(x.Id, out var item) ? item : null)
            .FirstOrDefault(x => x is not null);

        var context = contexts.ByCandidateKey.TryGetValue(candidateKey, out var value)
            ? value
            : ObservationContextSummary.Empty;

        return new CanonicalPersonCandidateDetailsDto
        {
            CandidateKey = candidateKey,
            DisplayName = resolvedRows.Select(x => x.Name.Trim()).First(),
            HasCanonicalPerson = primaryCanonical is not null,
            CanonicalPersonId = primaryCanonical?.Id,
            CanonicalDisplayName = primaryCanonical?.DisplayName,
            CanonicalNote = primaryCanonical?.Note,
            Warning = linkedCanonicalIds.Count > 1
                ? "Для цього імені вже існує більше однієї основної особи. Потрібна ручна перевірка."
                : null,
            Frequencies = [.. context.Frequencies],
            Divisions = [.. context.Divisions],
            Rows = [.. resolvedRows.Select(x =>
            {
                var rowKey = BuildObservationRowKey(x.Name, x.Frequency, x.Division);
                var rowObservationCount = contexts.ByRowKey.TryGetValue(rowKey, out var count)
                    ? count
                    : 0;

                return new CanonicalPersonCandidateRowDto
                {
                    ResolvedParticipantId = x.Id,
                    Name = x.Name,
                    Frequency = x.Frequency,
                    Role = x.Role,
                    Division = x.Division,
                    ConfirmedAtUtc = x.ConfirmedAtUtc,
                    IsLinkedToCanonical = canonicalMap.ContainsKey(x.Id),
                    ObservationCount = rowObservationCount
                };
            })]
        };
    }

    public async Task<CanonicalPersonCandidateDetailsDto> CreateCanonicalAsync(
        string candidateKey,
        IReadOnlyCollection<Guid> resolvedParticipantIds,
        string displayName,
        string? note,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(candidateKey))
            throw new ArgumentException("Ключ кандидата обов'язковий.", nameof(candidateKey));

        if (resolvedParticipantIds is null || resolvedParticipantIds.Count == 0)
            throw new InvalidOperationException("Оберіть хоча б один підтверджений рядок для створення основної особи.");

        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvalidOperationException("Назва основної особи обов'язкова.");

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
            throw new InvalidOperationException("Один або кілька рядків уже входять до іншої основної особи.");

        var canonical = CanonicalPerson.Create(displayName, note);
        foreach (var rowId in resolvedParticipantIds.Distinct())
            canonical.AddMember(rowId);

        db.CanonicalPersons.Add(canonical);
        await db.SaveChangesAsync(ct);

        return await GetCandidateDetailsAsync(candidateKey, ct)
            ?? throw new InvalidOperationException("Не вдалося перечитати створену основну особу.");
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
            ?? throw new InvalidOperationException("Основну особу не знайдено.");

        var selected = await db.ResolvedParticipants
            .Where(x => resolvedParticipantIds.Contains(x.Id))
            .ToListAsync(ct);

        if (selected.Count != resolvedParticipantIds.Count)
            throw new InvalidOperationException("Частину підтверджених рядків не знайдено.");

        var foreignMembers = await db.CanonicalPersonMembers
            .Where(x => resolvedParticipantIds.Contains(x.ResolvedParticipantId) && x.CanonicalPersonId != canonicalPersonId)
            .AnyAsync(ct);

        if (foreignMembers)
            throw new InvalidOperationException("Один або кілька рядків уже входять до іншої основної особи.");

        foreach (var rowId in resolvedParticipantIds.Distinct())
        {
            if (canonical.Members.Any(x => x.ResolvedParticipantId == rowId))
                continue;

            canonical.AddMember(rowId);
        }

        if (!string.IsNullOrWhiteSpace(note))
            canonical.Update(canonical.DisplayName, note);

        await db.SaveChangesAsync(ct);

        var candidateKey = NormalizeCandidateKey(selected.Select(x => x.Name).First());
        return await GetCandidateDetailsAsync(candidateKey, ct)
            ?? throw new InvalidOperationException("Не вдалося перечитати оновлену основну особу.");
    }

    public async Task<CanonicalPersonCandidateDetailsDto> UpdateCanonicalAsync(
        string candidateKey,
        Guid canonicalPersonId,
        string displayName,
        string? note,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(candidateKey))
            throw new ArgumentException("Ключ кандидата обов'язковий.", nameof(candidateKey));

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var canonical = await db.CanonicalPersons
            .FirstOrDefaultAsync(x => x.Id == canonicalPersonId, ct)
            ?? throw new InvalidOperationException("Основну особу не знайдено.");

        canonical.Update(displayName, note);
        await db.SaveChangesAsync(ct);

        return await GetCandidateDetailsAsync(candidateKey, ct)
            ?? throw new InvalidOperationException("Не вдалося перечитати оновлену основну особу.");
    }

    public async Task DeleteCanonicalAsync(Guid canonicalPersonId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var canonical = await db.CanonicalPersons
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Id == canonicalPersonId, ct)
            ?? throw new InvalidOperationException("Основну особу не знайдено.");

        db.CanonicalPersons.Remove(canonical);
        await db.SaveChangesAsync(ct);
    }

    private static string NormalizeCandidateKey(string? name)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(name)?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string NormalizeOptional(string? value)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(value) ?? string.Empty;

    private static string BuildObservationRowKey(string? name, string? frequency, string? division)
        => $"{NormalizeCandidateKey(name)}|{NormalizeOptional(frequency)}|{NormalizeOptional(division)}";

    private static async Task<ObservationContextStore> LoadObservationContextsAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.InterceptionMessages
            .AsNoTracking()
            .SelectMany(
                message => message.Participants,
                (message, participant) => new
                {
                    participant.Name,
                    participant.IsUnknown,
                    message.Frequency,
                    message.Division
                })
            .Where(x => !x.IsUnknown && x.Name != null && x.Name != "")
            .ToListAsync(ct);

        var byCandidateKey = rows
            .Select(x => new ObservationContextRow(x.Name, x.Frequency, x.Division))
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

        var byRowKey = rows
            .Select(x => BuildObservationRowKey(x.Name, x.Frequency, x.Division))
            .GroupBy(x => x)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        return new ObservationContextStore(byCandidateKey, byRowKey);
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

    private sealed record ResolvedRow(
        Guid Id,
        string Name,
        string? Frequency,
        string? Role,
        string? Division,
        DateTime ConfirmedAtUtc);

    private sealed record ObservationContextRow(string? Name, string? Frequency, string? Division);

    private sealed record ObservationContextSummary(
        IReadOnlyList<string> Frequencies,
        IReadOnlyList<string> Divisions,
        int ObservationCount)
    {
        public static ObservationContextSummary Empty { get; } = new([], [], 0);
    }

    private sealed record ObservationContextStore(
        IReadOnlyDictionary<string, ObservationContextSummary> ByCandidateKey,
        IReadOnlyDictionary<string, int> ByRowKey);

    private sealed record CanonicalLookupItem(Guid Id, string DisplayName, string? Note);
}
