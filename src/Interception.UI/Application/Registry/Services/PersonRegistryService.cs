//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Registry.Dtos;
using Interception.UI.Domain;
using Interception.UI.Domain.ValueObjects;
using Interception.UI.Extensions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Registry.Services;

/// <summary>
/// Сервіс реєстру осіб.
/// </summary>
public sealed class PersonRegistryService(
    IDbContextFactory<AppDbContext> dbFactory) : IPersonRegistryService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PersonRegistryItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var confirmedEntities = await db.ResolvedParticipants
            .AsNoTracking()
            .ToListAsync(ct);

        var confirmed = confirmedEntities
            .Select(x => new PersonRegistryItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Frequency = ReadFrequency(db, x),
                Role = x.Role,
                Division = x.Division,
                IsConfirmed = true,
                ConfirmedBy = x.ConfirmedBy,
                ConfirmedAt = x.ConfirmedAt
            })
            .ToList();

        var observedRows = await db.InterceptionMessages
            .AsNoTracking()
            .SelectMany(
                message => message.Participants,
                (message, participant) => new
                {
                    ParticipantId = participant.Id,
                    participant.Name,
                    participant.Role,
                    participant.IsUnknown,
                    message.Frequency,
                    message.Division
                })
            .Where(x => !x.IsUnknown && !string.IsNullOrWhiteSpace(x.Name))
            .ToListAsync(ct);

        var observed = observedRows
            .GroupBy(x => new
            {
                NameKey = NormalizeKey(x.Name),
                FrequencyKey = NormalizeKey(x.Frequency),
                DivisionKey = NormalizeKey(x.Division)
            })
            .Select(group => new PersonRegistryItemDto
            {
                Id = group.OrderBy(x => x.ParticipantId).Select(x => x.ParticipantId).First(),
                Name = group.Select(x => x.Name!.Trim()).First(),
                Frequency = group.Select(x => NormalizeOptional(x.Frequency)).FirstOrDefault(x => x is not null),
                Role = group.Select(x => NormalizeOptional(x.Role)).FirstOrDefault(x => x is not null),
                Division = group.Select(x => NormalizeOptional(x.Division)).FirstOrDefault(x => x is not null),
                IsConfirmed = false
            })
            .Where(x => !HasMatchingConfirmedContext(x, confirmed))
            .ToList();

        return [.. confirmed
            .Concat(observed)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Frequency, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Division, StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc />
    public async Task<PersonRegistryItemDto> UpdateAsync(Guid id, PersonRegistryUpdateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var normalizedName = PersonName.Create(dto.Name)?.Value
            ?? throw new InvalidOperationException("Ім'я особи обов'язкове.");
        var normalizedFrequency = FrequencyCode.Create(dto.Frequency)?.Value;
        var normalizedDivision = DivisionName.Create(dto.Division)?.Value;

        var roleMap = await ParticipantRoleCatalogSupport.LoadRoleMapAsync(db, ct);
        var normalizedRole = ParticipantRoleCatalogSupport.NormalizeRole(dto.Role, roleMap);

        var resolvedById = await db.ResolvedParticipants
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (resolvedById is not null)
        {
            resolvedById.Update(normalizedName, normalizedRole, normalizedDivision);
            db.Entry(resolvedById).Property("Frequency").CurrentValue = normalizedFrequency;

            await db.SaveChangesAsync(ct);

            return MapResolved(db, resolvedById);
        }

        var participant = await db.InterceptionMessageParticipants
            .Include(x => x.InterceptionMessage)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (participant is null || participant.IsUnknown || string.IsNullOrWhiteSpace(participant.Name))
            throw new InvalidOperationException($"Особу '{id}' не знайдено.");

        participant.ResolveAsKnown(normalizedName, normalizedRole);

        var effectiveFrequency = normalizedFrequency
            ?? NormalizeOptional(participant.InterceptionMessage.Frequency);

        var reusable = await FindReusableResolvedAsync(
            db,
            normalizedName,
            effectiveFrequency,
            normalizedDivision,
            ct);

        if (reusable is null)
        {
            reusable = ResolvedParticipant.Create(
                normalizedName,
                confirmedBy: "registry",
                role: normalizedRole,
                division: normalizedDivision);

            db.Entry(reusable).Property("Frequency").CurrentValue = effectiveFrequency;
            db.ResolvedParticipants.Add(reusable);
        }
        else
        {
            reusable.Update(normalizedName, normalizedRole, normalizedDivision);
            db.Entry(reusable).Property("Frequency").CurrentValue = effectiveFrequency;
        }

        await db.SaveChangesAsync(ct);

        return MapResolved(db, reusable);
    }

    private static async Task<ResolvedParticipant?> FindReusableResolvedAsync(
        AppDbContext db,
        string normalizedName,
        string? frequency,
        string? division,
        CancellationToken ct)
    {
        var allSameName = await db.ResolvedParticipants
            .AsTracking()
            .ToListAsync(ct);

        var sameName = allSameName
            .Where(x => string.Equals(
                NormalizeOptional(x.Name),
                NormalizeOptional(normalizedName),
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (sameName.Count == 0)
            return null;

        var exact = sameName.FirstOrDefault(x =>
            AreSameOptional(ReadFrequency(db, x), frequency) &&
            AreSameOptional(x.Division, division));

        if (exact is not null)
            return exact;

        var contextFree = sameName
            .Where(x =>
                string.IsNullOrWhiteSpace(ReadFrequency(db, x)) &&
                string.IsNullOrWhiteSpace(x.Division))
            .ToList();

        return contextFree.Count == 1
            ? contextFree[0]
            : null;
    }

    private static bool HasMatchingConfirmedContext(
        PersonRegistryItemDto observed,
        IReadOnlyList<PersonRegistryItemDto> confirmed)
    {
        var observedName = NormalizeOptional(observed.Name);
        var observedFrequency = NormalizeOptional(observed.Frequency);
        var observedDivision = NormalizeOptional(observed.Division);

        return confirmed.Any(item =>
        {
            if (!string.Equals(NormalizeOptional(item.Name), observedName, StringComparison.OrdinalIgnoreCase))
                return false;

            var confirmedFrequency = NormalizeOptional(item.Frequency);
            var confirmedDivision = NormalizeOptional(item.Division);

            if (!string.IsNullOrWhiteSpace(observedFrequency)
                && !string.IsNullOrWhiteSpace(confirmedFrequency)
                && !string.Equals(observedFrequency, confirmedFrequency, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(observedDivision)
                && !string.IsNullOrWhiteSpace(confirmedDivision)
                && !string.Equals(observedDivision, confirmedDivision, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        });
    }

    private static PersonRegistryItemDto MapResolved(AppDbContext db, ResolvedParticipant resolved)
        => new()
        {
            Id = resolved.Id,
            Name = resolved.Name,
            Frequency = ReadFrequency(db, resolved),
            Role = resolved.Role,
            Division = resolved.Division,
            IsConfirmed = true,
            ConfirmedBy = resolved.ConfirmedBy,
            ConfirmedAt = resolved.ConfirmedAt
        };

    private static string? ReadFrequency(AppDbContext db, ResolvedParticipant resolved)
        => NormalizeOptional(db.Entry(resolved).Property<string?>("Frequency").CurrentValue);

    private static bool AreSameOptional(string? left, string? right)
        => string.Equals(
            NormalizeOptional(left),
            NormalizeOptional(right),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeKey(string? value)
        => NormalizeOptional(value)?.ToUpperInvariant() ?? string.Empty;

    private static string? NormalizeOptional(string? value)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(value);
}
