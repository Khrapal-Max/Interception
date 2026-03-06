//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Interception.UI.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Observations.Services;

public sealed class ObservationWriteService(IDbContextFactory<AppDbContext> dbFactory) : IObservationWriteService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<ObservationCreateResultDto> CreateAsync(ObservationCreateRequestDto request, CancellationToken ct)
    {
        await using var db = _dbFactory.CreateDbContext();

        // Normalize/sanitize user input here (UI should be dumb).
        var actionRaw = (request.ActionRaw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(actionRaw))
            throw new ArgumentException("Поле 'Дія' є обов'язковим.", nameof(request));        

        var normalizedParticipants = (request.Participants ?? [])
            .Select(p =>
            {
                var label = Norm(p.LabelRaw);
                var role = Norm(p.RoleRaw);

                // Detect unknown by checkbox OR by "НВ"/"NV"/"unknown" label.
                var labelNorm = TextNorm.Normalize(label);
                var unknownByLabel = labelNorm is "нв" or "nv" or "unknown";
                var isUnknown = p.IsUnknown || unknownByLabel;

                // Canonical display for unknown.
                if (isUnknown)
                    label = "НВ";

                // Skip completely empty rows (no signal at all).
                if (label is null && role is null && !isUnknown)
                    return null;

                return new ObservationCreateParticipantDto(label, isUnknown, role);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        // Build domain entity
        var obs = Observation.Create(
            request.ObservedDate,
            (Domain.Enums.DayPart)request.DayPart,
            actionRaw,
            layer: Norm(request.Layer),
            rmRaw: Norm(request.RmRaw),
            pointRaw: Norm(request.PointRaw),
            locationRaw: Norm(request.LocationRaw),
            districtRaw: Norm(request.DistrictRaw),
            note: Norm(request.Note),
            source: "manual",
            sourceFileId: null,
            sourceRow: null,
            createdBy: null);

        if (normalizedParticipants.Count > 0)
        {
            var ord = 1;
            foreach (var p in normalizedParticipants)
            {
                obs.AddParticipant(p.LabelRaw, p.IsUnknown, p.RoleRaw, ord);
                ord++;
            }
        }

        db.Observations.Add(obs);

        try
        {
            await db.SaveChangesAsync(ct);
            return new ObservationCreateResultDto(obs.Id, IsDuplicate: false);
        }
        catch (DbUpdateException ex)
        {
            // Duplicate by ContentHash (unique index)
            return new ObservationCreateResultDto(null, IsDuplicate: true);
        }
    }

    private static string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

