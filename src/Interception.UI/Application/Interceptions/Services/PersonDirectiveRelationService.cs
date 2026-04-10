//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Interceptions.Services;

/// <summary>
/// Простий сервіс ручного ведення контуру структурного керування.
/// </summary>
public sealed class PersonDirectiveRelationService(IDbContextFactory<AppDbContext> dbFactory)
    : IPersonDirectiveRelationService
{
    public async Task<IReadOnlyList<PersonDirectiveRelationListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db.PersonDirectiveRelations
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return [];

        var canonicalIds = rows
            .SelectMany(x => new[] { x.FromCanonicalPersonId, x.ToCanonicalPersonId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var resolvedIds = rows
            .SelectMany(x => new[] { x.FromResolvedParticipantId, x.ToResolvedParticipantId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var canonicalMap = await db.CanonicalPersons
            .AsNoTracking()
            .Where(x => canonicalIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => SemanticValueExtensions.NormalizeMeaningfulOrNull(x.DisplayName) ?? "—",
                ct);

        var resolvedMap = await db.ResolvedParticipants
            .AsNoTracking()
            .Where(x => resolvedIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => BuildResolvedLabel(x.Name, x.Role, x.Division, x.Frequency),
                ct);

        return [.. rows.Select(x => new PersonDirectiveRelationListItemDto
        {
            Id = x.Id,
            FromCanonicalPersonId = x.FromCanonicalPersonId,
            FromResolvedParticipantId = x.FromResolvedParticipantId,
            FromDisplayName = ResolveEndpointLabel(x.FromCanonicalPersonId, x.FromResolvedParticipantId, canonicalMap, resolvedMap),
            ToCanonicalPersonId = x.ToCanonicalPersonId,
            ToResolvedParticipantId = x.ToResolvedParticipantId,
            ToDisplayName = ResolveEndpointLabel(x.ToCanonicalPersonId, x.ToResolvedParticipantId, canonicalMap, resolvedMap),
            RelationType = x.RelationType,
            Confidence = x.Confidence,
            SourceObservationId = x.SourceObservationId,
            IsManual = x.IsManual,
            Comment = x.Comment,
            UpdatedAtUtc = x.UpdatedAtUtc
        })];
    }

    public async Task<IReadOnlyList<PersonDirectiveRelationOptionDto>> GetIdentityOptionsAsync(
        IReadOnlyList<string>? participantNames = null,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var normalizedNames = (participantNames ?? [])
            .Select(NormalizeKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalizedNames.Count == 0)
            return await GetAllIdentityOptionsAsync(db, ct);

        var canonicalMemberships = await db.CanonicalPersonMembers
            .AsNoTracking()
            .Include(x => x.CanonicalPerson)
            .Include(x => x.ResolvedParticipant)
            .ToListAsync(ct);

        var canonicalRows = canonicalMemberships
            .Where(x => normalizedNames.Contains(NormalizeKey(x.ResolvedParticipant.Name)))
            .GroupBy(x => x.CanonicalPersonId)
            .Select(group =>
            {
                var first = group.First();
                return new PersonDirectiveRelationOptionDto
                {
                    IdentityId = first.CanonicalPersonId,
                    IsCanonicalPerson = true,
                    DisplayName = first.CanonicalPerson.DisplayName,
                    KindLabel = "Об’єднаний профіль"
                };
            })
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var linkedResolvedIds = canonicalMemberships
            .Select(x => x.ResolvedParticipantId)
            .Distinct()
            .ToHashSet();

        var standaloneResolvedRows = await db.ResolvedParticipants
            .AsNoTracking()
            .Where(x => !linkedResolvedIds.Contains(x.Id))
            .ToListAsync(ct);

        var standaloneOptions = standaloneResolvedRows
            .Where(x => normalizedNames.Contains(NormalizeKey(x.Name)))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Division, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Frequency, StringComparer.OrdinalIgnoreCase)
            .Select(x => new PersonDirectiveRelationOptionDto
            {
                IdentityId = x.Id,
                IsCanonicalPerson = false,
                DisplayName = BuildResolvedLabel(x.Name, x.Role, x.Division, x.Frequency),
                Role = x.Role,
                Division = x.Division,
                Frequency = x.Frequency,
                KindLabel = "Підтверджена особа"
            })
            .ToList();

        return [.. canonicalRows, .. standaloneOptions];
    }

    public async Task SaveAsync(PersonDirectiveRelationSaveDto dto, CancellationToken ct = default)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));

        ValidateDto(dto);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        await EnsureEndpointExistsAsync(db, dto.FromCanonicalPersonId, dto.FromResolvedParticipantId, ct);
        await EnsureEndpointExistsAsync(db, dto.ToCanonicalPersonId, dto.ToResolvedParticipantId, ct);

        var existing = await db.PersonDirectiveRelations
            .FirstOrDefaultAsync(
                x => x.FromCanonicalPersonId == Normalize(dto.FromCanonicalPersonId)
                  && x.FromResolvedParticipantId == Normalize(dto.FromResolvedParticipantId)
                  && x.ToCanonicalPersonId == Normalize(dto.ToCanonicalPersonId)
                  && x.ToResolvedParticipantId == Normalize(dto.ToResolvedParticipantId),
                ct);

        if (existing is null)
        {
            db.PersonDirectiveRelations.Add(PersonDirectiveRelation.Create(
                dto.FromCanonicalPersonId,
                dto.FromResolvedParticipantId,
                dto.ToCanonicalPersonId,
                dto.ToResolvedParticipantId,
                dto.RelationType,
                dto.Confidence,
                dto.SourceObservationId,
                dto.IsManual,
                dto.Comment));
        }
        else
        {
            existing.Update(
                dto.RelationType,
                dto.Confidence,
                dto.SourceObservationId,
                dto.IsManual,
                dto.Comment);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var relation = await db.PersonDirectiveRelations
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Зв'язок керування не знайдено.");

        db.PersonDirectiveRelations.Remove(relation);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<IReadOnlyList<PersonDirectiveRelationOptionDto>> GetAllIdentityOptionsAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var canonicalRows = await db.CanonicalPersons
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(x => new PersonDirectiveRelationOptionDto
            {
                IdentityId = x.Id,
                IsCanonicalPerson = true,
                DisplayName = x.DisplayName,
                KindLabel = "Об’єднаний профіль"
            })
            .ToListAsync(ct);

        var linkedResolvedIds = await db.CanonicalPersonMembers
            .AsNoTracking()
            .Select(x => x.ResolvedParticipantId)
            .Distinct()
            .ToListAsync(ct);

        var standaloneResolvedRows = await db.ResolvedParticipants
            .AsNoTracking()
            .Where(x => !linkedResolvedIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Division)
            .ThenBy(x => x.Frequency)
            .Select(x => new PersonDirectiveRelationOptionDto
            {
                IdentityId = x.Id,
                IsCanonicalPerson = false,
                DisplayName = BuildResolvedLabel(x.Name, x.Role, x.Division, x.Frequency),
                Role = x.Role,
                Division = x.Division,
                Frequency = x.Frequency,
                KindLabel = "Підтверджена особа"
            })
            .ToListAsync(ct);

        return [.. canonicalRows, .. standaloneResolvedRows];
    }

    private static async Task EnsureEndpointExistsAsync(
        AppDbContext db,
        Guid? canonicalPersonId,
        Guid? resolvedParticipantId,
        CancellationToken ct)
    {
        canonicalPersonId = Normalize(canonicalPersonId);
        resolvedParticipantId = Normalize(resolvedParticipantId);

        if (canonicalPersonId.HasValue)
        {
            var existsCanonical = await db.CanonicalPersons.AnyAsync(x => x.Id == canonicalPersonId.Value, ct);
            if (!existsCanonical)
                throw new InvalidOperationException("Об’єднаний профіль не знайдено.");

            return;
        }

        if (resolvedParticipantId.HasValue)
        {
            var existsResolved = await db.ResolvedParticipants.AnyAsync(x => x.Id == resolvedParticipantId.Value, ct);
            if (!existsResolved)
                throw new InvalidOperationException("Підтверджену особу не знайдено.");

            return;
        }

        throw new InvalidOperationException("Потрібно вибрати особу.");
    }

    private static void ValidateDto(PersonDirectiveRelationSaveDto dto)
    {
        var fromCount = (Normalize(dto.FromCanonicalPersonId).HasValue ? 1 : 0) + (Normalize(dto.FromResolvedParticipantId).HasValue ? 1 : 0);
        var toCount = (Normalize(dto.ToCanonicalPersonId).HasValue ? 1 : 0) + (Normalize(dto.ToResolvedParticipantId).HasValue ? 1 : 0);

        if (fromCount != 1 || toCount != 1)
            throw new InvalidOperationException("Потрібно вибрати по одній особі з кожного боку.");

        var sameCanonical = Normalize(dto.FromCanonicalPersonId).HasValue
            && Normalize(dto.ToCanonicalPersonId).HasValue
            && Normalize(dto.FromCanonicalPersonId) == Normalize(dto.ToCanonicalPersonId);

        var sameResolved = Normalize(dto.FromResolvedParticipantId).HasValue
            && Normalize(dto.ToResolvedParticipantId).HasValue
            && Normalize(dto.FromResolvedParticipantId) == Normalize(dto.ToResolvedParticipantId);

        if (sameCanonical || sameResolved)
            throw new InvalidOperationException("Зв'язок особи із самою собою не підтримується.");
    }

    private static Guid? Normalize(Guid? value)
        => value.HasValue && value.Value != Guid.Empty ? value.Value : null;

    private static string ResolveEndpointLabel(
        Guid? canonicalPersonId,
        Guid? resolvedParticipantId,
        IReadOnlyDictionary<Guid, string> canonicalMap,
        IReadOnlyDictionary<Guid, string> resolvedMap)
    {
        if (canonicalPersonId.HasValue && canonicalMap.TryGetValue(canonicalPersonId.Value, out var canonicalName))
            return canonicalName;

        if (resolvedParticipantId.HasValue && resolvedMap.TryGetValue(resolvedParticipantId.Value, out var resolvedName))
            return resolvedName;

        return "—";
    }

    private static string BuildResolvedLabel(string? name, string? role, string? division, string? frequency)
    {
        var parts = new List<string>();

        var normalizedName = SemanticValueExtensions.NormalizeMeaningfulOrNull(name);
        var normalizedRole = SemanticValueExtensions.NormalizeMeaningfulOrNull(role);
        var normalizedDivision = SemanticValueExtensions.NormalizeMeaningfulOrNull(division);
        var normalizedFrequency = SemanticValueExtensions.NormalizeMeaningfulOrNull(frequency);

        if (!string.IsNullOrWhiteSpace(normalizedName))
            parts.Add(normalizedName);

        if (!string.IsNullOrWhiteSpace(normalizedRole))
            parts.Add(normalizedRole);

        if (!string.IsNullOrWhiteSpace(normalizedDivision))
            parts.Add(normalizedDivision);

        if (!string.IsNullOrWhiteSpace(normalizedFrequency))
            parts.Add(normalizedFrequency);

        return parts.Count == 0 ? "—" : string.Join(" • ", parts);
    }

    private static string NormalizeKey(string? value)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(value)?.Trim().ToUpperInvariant() ?? string.Empty;
}
