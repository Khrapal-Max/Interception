//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Registry.Dtos;
using Interception.UI.Domain;
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

        var confirmed = await db.ResolvedParticipants
            .AsNoTracking()
            .Select(x => new PersonRegistryItemDto
            {
                Id = x.Id,
                Name = x.Name,
                Frequency = EF.Property<string?>(x, "Frequency"),
                Role = x.Role,
                Division = x.Division,
                IsConfirmed = true,
                ConfirmedBy = x.ConfirmedBy,
                ConfirmedAt = x.ConfirmedAt
            })
            .ToListAsync(ct);

        var singleConfirmedByName = confirmed
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);

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
            .Where(x =>
                !x.IsUnknown &&
                !string.IsNullOrWhiteSpace(x.Name))
            .ToListAsync(ct);

        var observed = observedRows
            .GroupBy(x => new
            {
                NameKey = x.Name!.Trim().ToUpperInvariant(),
                FrequencyKey = (SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Frequency) ?? string.Empty).ToUpperInvariant(),
                DivisionKey = (SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Division) ?? string.Empty).ToUpperInvariant()
            })
            .Select(group => new PersonRegistryItemDto
            {
                Id = group.OrderBy(x => x.ParticipantId).Select(x => x.ParticipantId).First(),
                Name = group.Select(x => x.Name!.Trim()).First(),
                Frequency = string.IsNullOrWhiteSpace(group.Key.FrequencyKey)
                    ? null
                    : group.Select(x => SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Frequency))
                        .FirstOrDefault(x => x is not null),
                Role = group.Select(x => SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Role))
                    .FirstOrDefault(x => x is not null),
                Division = string.IsNullOrWhiteSpace(group.Key.DivisionKey)
                    ? null
                    : group.Select(x => SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Division))
                        .FirstOrDefault(x => x is not null),
                IsConfirmed = false,
                ConfirmedBy = null,
                ConfirmedAt = null
            })
            .Where(x => !ShouldHideObservedRow(x, singleConfirmedByName))
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

        var normalizedName = dto.Name.Trim();
        var normalizedFrequency = SemanticValueExtensions.NormalizeMeaningfulOrNull(dto.Frequency);
        var roleMap = await ParticipantRoleCatalogSupport.LoadRoleMapAsync(db, ct);
        var normalizedRole = ParticipantRoleCatalogSupport.NormalizeRole(dto.Role, roleMap);
        var normalizedDivision = SemanticValueExtensions.NormalizeMeaningfulOrNull(dto.Division);

        var resolved = await db.ResolvedParticipants
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (resolved is not null)
        {
            resolved.Update(normalizedName, normalizedRole, normalizedDivision);
            db.Entry(resolved).Property("Frequency").CurrentValue = normalizedFrequency;
            await db.SaveChangesAsync(ct);

            return new PersonRegistryItemDto
            {
                Id = resolved.Id,
                Name = resolved.Name,
                Frequency = normalizedFrequency,
                Role = resolved.Role,
                Division = resolved.Division,
                IsConfirmed = true,
                ConfirmedBy = resolved.ConfirmedBy,
                ConfirmedAt = resolved.ConfirmedAt
            };
        }

        var participant = await db.InterceptionMessageParticipants
            .Include(x => x.InterceptionMessage)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (participant is null || participant.IsUnknown || string.IsNullOrWhiteSpace(participant.Name))
            throw new InvalidOperationException($"Особу '{id}' не знайдено.");

        participant.ResolveAsKnown(normalizedName, normalizedRole);

        var effectiveFrequency = normalizedFrequency
            ?? SemanticValueExtensions.NormalizeMeaningfulOrNull(participant.InterceptionMessage.Frequency);

        var created = ResolvedParticipant.Create(
            normalizedName,
            confirmedBy: "registry",
            role: normalizedRole,
            division: normalizedDivision);

        db.Entry(created).Property("Frequency").CurrentValue = effectiveFrequency;

        db.ResolvedParticipants.Add(created);
        await db.SaveChangesAsync(ct);

        return new PersonRegistryItemDto
        {
            Id = created.Id,
            Name = created.Name,
            Frequency = effectiveFrequency,
            Role = created.Role,
            Division = created.Division,
            IsConfirmed = true,
            ConfirmedBy = created.ConfirmedBy,
            ConfirmedAt = created.ConfirmedAt
        };
    }

    /// <summary>
    /// Визначає, чи треба приховати observed-рядок, якщо для нього вже існує
    /// однозначний confirmed row.
    /// </summary>
    private static bool ShouldHideObservedRow(
        PersonRegistryItemDto observed,
        Dictionary<string, PersonRegistryItemDto> singleConfirmedByName)
    {
        if (!singleConfirmedByName.TryGetValue(observed.Name, out var confirmed))
            return false;

        var observedFrequency = SemanticValueExtensions.NormalizeMeaningfulOrNull(observed.Frequency);
        var confirmedFrequency = SemanticValueExtensions.NormalizeMeaningfulOrNull(confirmed.Frequency);

        if (!string.IsNullOrWhiteSpace(observedFrequency) && !string.IsNullOrWhiteSpace(confirmedFrequency))
        {
            if (!string.Equals(observedFrequency, confirmedFrequency, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        var observedDivision = SemanticValueExtensions.NormalizeMeaningfulOrNull(observed.Division);
        var confirmedDivision = SemanticValueExtensions.NormalizeMeaningfulOrNull(confirmed.Division);

        if (observedDivision is null)
            return true;

        if (confirmedDivision is null)
            return true;

        return string.Equals(
            observedDivision,
            confirmedDivision,
            StringComparison.OrdinalIgnoreCase);
    }
}
