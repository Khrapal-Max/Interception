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

        var supportsResolvedFrequency = SupportsResolvedFrequency(db);

        var confirmed = supportsResolvedFrequency
            ? await db.ResolvedParticipants
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
                .ToListAsync(ct)
            : await db.ResolvedParticipants
                .AsNoTracking()
                .Select(x => new PersonRegistryItemDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Frequency = null,
                    Role = x.Role,
                    Division = x.Division,
                    IsConfirmed = true,
                    ConfirmedBy = x.ConfirmedBy,
                    ConfirmedAt = x.ConfirmedAt
                })
                .ToListAsync(ct);

        var confirmedByName = confirmed
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<PersonRegistryItemDto>)x.ToList(), StringComparer.OrdinalIgnoreCase);

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
                 Frequency = group.Select(x => SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Frequency))
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
             .Where(x => !ShouldHideObservedRow(x, confirmedByName))
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
        var normalizedRole = SemanticValueExtensions.NormalizeMeaningfulOrNull(dto.Role);
        var normalizedDivision = SemanticValueExtensions.NormalizeMeaningfulOrNull(dto.Division);
        var supportsResolvedFrequency = SupportsResolvedFrequency(db);

        var resolved = await db.ResolvedParticipants
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (resolved is not null)
        {
            resolved.Update(normalizedName, normalizedRole, normalizedDivision);
            await db.SaveChangesAsync(ct);

            string? resolvedFrequency = null;
            if (supportsResolvedFrequency)
            {
                resolvedFrequency = await db.ResolvedParticipants
                    .Where(x => x.Id == id)
                    .Select(x => EF.Property<string?>(x, "Frequency"))
                    .FirstOrDefaultAsync(ct);
            }

            return new PersonRegistryItemDto
            {
                Id = resolved.Id,
                Name = resolved.Name,
                Frequency = SemanticValueExtensions.NormalizeMeaningfulOrNull(resolvedFrequency),
                Role = resolved.Role,
                Division = resolved.Division,
                IsConfirmed = true,
                ConfirmedBy = resolved.ConfirmedBy,
                ConfirmedAt = resolved.ConfirmedAt
            };
        }

        var participantContext = await db.InterceptionMessageParticipants
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                ParticipantId = x.Id,
                x.Name,
                x.IsUnknown,
                MessageFrequency = x.InterceptionMessage.Frequency
            })
            .FirstOrDefaultAsync(ct);

        if (participantContext is null || participantContext.IsUnknown || string.IsNullOrWhiteSpace(participantContext.Name))
            throw new InvalidOperationException($"Особу '{id}' не знайдено.");

        var participant = await db.InterceptionMessageParticipants
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (participant is null)
            throw new InvalidOperationException($"Особу '{id}' не знайдено.");

        participant.ResolveAsKnown(normalizedName, normalizedRole);

        var created = ResolvedParticipant.Create(
            normalizedName,
            confirmedBy: "registry",
            role: normalizedRole,
            division: normalizedDivision);

        db.ResolvedParticipants.Add(created);

        if (supportsResolvedFrequency)
        {
            var normalizedFrequency = SemanticValueExtensions.NormalizeMeaningfulOrNull(participantContext.MessageFrequency);
            db.Entry(created).Property("Frequency").CurrentValue = normalizedFrequency;
        }

        await db.SaveChangesAsync(ct);

        return new PersonRegistryItemDto
        {
            Id = created.Id,
            Name = created.Name,
            Frequency = supportsResolvedFrequency
                ? SemanticValueExtensions.NormalizeMeaningfulOrNull(participantContext.MessageFrequency)
                : null,
            Role = created.Role,
            Division = created.Division,
            IsConfirmed = true,
            ConfirmedBy = created.ConfirmedBy,
            ConfirmedAt = created.ConfirmedAt
        };
    }

    /// <summary>
    /// Визначає, чи треба приховати observed-рядок, якщо для нього вже існує
    /// підтверджений рядок з таким самим контекстом.
    /// </summary>
    private static bool ShouldHideObservedRow(
        PersonRegistryItemDto observed,
        Dictionary<string, IReadOnlyList<PersonRegistryItemDto>> confirmedByName)
    {
        if (!confirmedByName.TryGetValue(observed.Name, out var confirmedRows) || confirmedRows.Count == 0)
            return false;

        var observedFrequency = SemanticValueExtensions.NormalizeMeaningfulOrNull(observed.Frequency);
        var observedRole = SemanticValueExtensions.NormalizeMeaningfulOrNull(observed.Role);
        var observedDivision = SemanticValueExtensions.NormalizeMeaningfulOrNull(observed.Division);

        var useFrequency = !string.IsNullOrWhiteSpace(observedFrequency)
            || confirmedRows.Any(x => !string.IsNullOrWhiteSpace(SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Frequency)));

        if (confirmedRows.Any(x =>
                (!useFrequency || StringComparer.OrdinalIgnoreCase.Equals(
                    SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Frequency),
                    observedFrequency)) &&
                StringComparer.OrdinalIgnoreCase.Equals(
                    SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Role),
                    observedRole) &&
                (StringComparer.OrdinalIgnoreCase.Equals(
                    SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Division),
                    observedDivision) ||
                 string.IsNullOrWhiteSpace(observedDivision))))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(observedDivision) && !string.IsNullOrWhiteSpace(observedRole))
        {
            var sameContext = confirmedRows
                .Where(x =>
                    (!useFrequency || StringComparer.OrdinalIgnoreCase.Equals(
                        SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Frequency),
                        observedFrequency)) &&
                    StringComparer.OrdinalIgnoreCase.Equals(
                        SemanticValueExtensions.NormalizeMeaningfulOrNull(x.Role),
                        observedRole))
                .ToList();

            if (sameContext.Count == 1)
                return true;
        }

        return false;
    }

    private static bool SupportsResolvedFrequency(AppDbContext db)
        => db.Model.FindEntityType(typeof(ResolvedParticipant))?.FindProperty("Frequency") is not null;
}
