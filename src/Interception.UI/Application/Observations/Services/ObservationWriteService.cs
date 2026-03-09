//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Observations.Services;

public sealed class ObservationWriteService(IDbContextFactory<AppDbContext> dbFactory) : IObservationWriteService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<ObservationCreateResultDto> CreateAsync(ObservationCreateRequestDto request, CancellationToken ct)
    {
        await using var db = _dbFactory.CreateDbContext();

        var action = NormalizeRequired(request.ActionRaw, "Action");
        var layer = NormalizeOptional(request.Layer);
        var rm = NormalizeOptional(request.RmRaw);
        var point = NormalizeOptional(request.PointRaw);
        var location = NormalizeOptional(request.LocationRaw);
        var district = NormalizeOptional(request.DistrictRaw);
        var note = NormalizeOptional(request.Note);

        var obs = Observation.Create(
            request.ObservedDate,
            (Domain.Enums.DayPart)request.DayPart,
            action,
            layer: layer,
            rmRaw: rm,
            pointRaw: point,
            locationRaw: location,
            districtRaw: district,
            companyRaw: null,
            note: note,
            source: "manual",
            sourceFileId: null,
            sourceRow: null,
            createdBy: null);

        if (request.Participants is not null)
        {
            var ord = 1;
            foreach (var p in request.Participants)
            {
                var label = NormalizeOptional(p.LabelRaw);

                // Unknown визначається на бекенді, але operator raw-label зберігаємо як є.
                // Не перетираємо "НВ 1" / "НВ 2" / "НВ 4" в одне значення "НВ".
                var isUnknown = p.IsUnknown || IsUnknownLabel(label);

                var role = NormalizeOptional(p.RoleRaw);

                // Skip fully empty participant rows
                if (label is null && role is null && !isUnknown)
                    continue;

                obs.AddParticipant(label, isUnknown, role, ord);
                ord++;
            }
        }

        db.Observations.Add(obs);

        try
        {
            await db.SaveChangesAsync(ct);
            return new ObservationCreateResultDto(obs.Id, IsDuplicate: false);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Unique violation on content_hash -> duplicate
            return new ObservationCreateResultDto(Guid.Empty, IsDuplicate: true);
        }
    }

    private static string NormalizeRequired(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{field} is required.");
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsUnknownLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return false;
        var x = label.Trim();
        return string.Equals(x, "НВ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x, "NV", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x, "UNKNOWN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x, "UNK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x, "?", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null) return false;

        var t = inner.GetType();
        if (!t.Name.Contains("Postgres", StringComparison.OrdinalIgnoreCase))
            return false;

        var prop = t.GetProperty("SqlState");
        var sqlState = prop?.GetValue(inner) as string;

        return string.Equals(sqlState, "23505", StringComparison.OrdinalIgnoreCase);
    }
}
