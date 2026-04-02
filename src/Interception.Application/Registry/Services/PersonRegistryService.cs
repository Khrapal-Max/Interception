//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Application.Registry.Abstractions;
using Interception.Application.Registry.Dtos;
using Interception.Common.Extensions;
using Interception.Domain.Entities;
using Interception.Infrastructure;
using Interception.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace Interception.Application.Registry.Services;

/// <summary>
/// Сервіс реєстру осіб.
/// </summary>
public sealed class PersonRegistryService(
    IDbContextFactory<PostgreSqlDbContext> dbFactory) : IPersonRegistryService
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
                 DivisionKey = (StringSemanticValueExtensions.NormalizeMeaningfulOrNull(x.Division) ?? string.Empty).ToUpperInvariant()
             })
             .Select(group => new PersonRegistryItemDto
             {
                 Id = group.OrderBy(x => x.ParticipantId).Select(x => x.ParticipantId).First(),
                 Name = group.Select(x => x.Name!.Trim()).First(),
                 Role = group.Select(x => StringSemanticValueExtensions.NormalizeMeaningfulOrNull(x.Role))
                     .FirstOrDefault(x => x is not null),
                 Division = string.IsNullOrWhiteSpace(group.Key.DivisionKey)
                     ? null
                     : group.Select(x => StringSemanticValueExtensions.NormalizeMeaningfulOrNull(x.Division))
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
             .ThenBy(x => x.Division, StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc />
    public async Task<PersonRegistryItemDto> UpdateAsync(Guid id, PersonRegistryUpdateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var normalizedName = dto.Name.Trim();
        var normalizedRole = StringSemanticValueExtensions.NormalizeMeaningfulOrNull(dto.Role);
        var normalizedDivision = StringSemanticValueExtensions.NormalizeMeaningfulOrNull(dto.Division);

        var resolved = await db.ResolvedParticipants
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (resolved is not null)
        {
            resolved.Update(normalizedName, normalizedRole, normalizedDivision);
            await db.SaveChangesAsync(ct);

            return new PersonRegistryItemDto
            {
                Id = resolved.Id,
                Name = resolved.Name,
                Role = resolved.Role,
                Division = resolved.Division,
                IsConfirmed = true,
                ConfirmedBy = resolved.ConfirmedBy,
                ConfirmedAt = resolved.ConfirmedAt
            };
        }

        var participant = await db.InterceptionMessageParticipants
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (participant is null || participant.IsUnknown || string.IsNullOrWhiteSpace(participant.Name))
            throw new InvalidOperationException($"Особу '{id}' не знайдено.");

        participant.ResolveAsKnown(normalizedName, normalizedRole);

        var created = ResolvedParticipant.Create(
            normalizedName,
            confirmedBy: "registry",
            role: normalizedRole,
            division: normalizedDivision);

        db.ResolvedParticipants.Add(created);
        await db.SaveChangesAsync(ct);

        return new PersonRegistryItemDto
        {
            Id = created.Id,
            Name = created.Name,
            Role = created.Role,
            Division = created.Division,
            IsConfirmed = true,
            ConfirmedBy = created.ConfirmedBy,
            ConfirmedAt = created.ConfirmedAt
        };
    }

    /// <summary>
    /// Визначає, чи треба приховати observed-рядок, якщо для нього вже існує
    /// однозначний canonical person.
    /// </summary>
    private static bool ShouldHideObservedRow(
        PersonRegistryItemDto observed,
        Dictionary<string, PersonRegistryItemDto> singleConfirmedByName)
    {
        if (!singleConfirmedByName.TryGetValue(observed.Name, out var confirmed))
            return false;

        var observedDivision = StringSemanticValueExtensions.NormalizeMeaningfulOrNull(observed.Division);
        var confirmedDivision = StringSemanticValueExtensions.NormalizeMeaningfulOrNull(confirmed.Division);

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
