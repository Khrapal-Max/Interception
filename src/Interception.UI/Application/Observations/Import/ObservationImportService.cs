//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Application.Observations.Import;

public sealed class ObservationImportService(IDbContextFactory<AppDbContext> dbFactory)
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

    public async Task<ObservationImportResult> ImportXlsxAsync(
        Stream xlsxStream,
        ObservationImportOptions options,
        CancellationToken ct)
    {
        var parser = new XlsxObservationImportParser();
        var rows = await parser.ParseAsync(xlsxStream, ct);
        return await ImportAsync(rows, options, ct);
    }

    public async Task<ObservationImportResult> ImportAsync(
            IReadOnlyList<ObservationImportRow> rows,
            ObservationImportOptions options,
            CancellationToken ct)
    {
        await using var db = _dbFactory.CreateDbContext();

        var result = new ObservationImportResult { TotalRows = rows.Count };

        // 1) Build observations & hash list
        var built = new List<(Observation obs, string hash, int sourceRowNumber)>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var r = rows[i];
            var rowNo = options.FirstSourceRowNumber + i;

            if (string.IsNullOrWhiteSpace(r.ActionRaw))
            {
                result.InvalidSkipped++;
                result.Errors.Add(new ObservationImportError(rowNo, "ActionRaw is empty."));
                continue;
            }

            try
            {
                var obs = Observation.Create(
                    r.ObservedDate,
                    (Domain.Enums.DayPart)r.DayPart,
                    r.ActionRaw,
                    layer: r.Layer,
                    rmRaw: r.RmRaw,
                    pointRaw: r.PointRaw,
                    locationRaw: r.LocationRaw,
                    districtRaw: r.DistrictRaw,
                    companyRaw: r.CompanyRaw,
                    note: r.Note,
                    source: options.Source,
                    sourceFileId: options.SourceFileId,
                    sourceRow: rowNo,
                    createdBy: options.CreatedBy);

                // participants can be 0..N
                if (r.Participants is not null)
                {
                    var ord = 1;
                    foreach (var p in r.Participants)
                    {
                        obs.AddParticipant(p.LabelRaw, p.IsUnknown, p.RoleRaw, ord);
                        ord++;
                    }
                }

                built.Add((obs, obs.ContentHash, rowNo));
            }
            catch (Exception ex)
            {
                result.InvalidSkipped++;
                result.Errors.Add(new ObservationImportError(rowNo, ex.Message));
            }
        }

        if (built.Count == 0)
            return result;

        // 2) Dedup inside batch by ContentHash (keep first occurrence)
        var seenInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<(Observation obs, string hash, int rowNo)>(built.Count);
        foreach (var (obs, hash, sourceRowNumber) in built)
        {
            if (!seenInBatch.Add(hash))
            {
                result.DuplicatesSkipped++;
                continue;
            }
            unique.Add((obs, hash, sourceRowNumber));
        }

        // 3) Dedup vs DB by ContentHash (fast path)
        HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase);
        if (options.DeduplicateByHash)
        {
            var hashes = unique.Select(x => x.hash).ToList();

            var existingHashes = await db.Observations
                .AsNoTracking()
                .Where(o => hashes.Contains(o.ContentHash))
                .Select(o => o.ContentHash)
                .ToListAsync(ct);

            existing = existingHashes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var toInsert = unique
            .Where(x => !existing.Contains(x.hash))
            .ToList();

        result.DuplicatesSkipped += unique.Count - toInsert.Count;

        if (toInsert.Count == 0)
            return result;

        // 4) Insert. We still keep a safety net for race conditions (unique index on content_hash).
        db.AddRange(toInsert.Select(x => x.obs));

        try
        {
            await db.SaveChangesAsync(ct);
            result.Inserted += toInsert.Count;
            return result;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Fallback: retry per row to isolate duplicates
            db.ChangeTracker.Clear();

            foreach (var (obs, hash, rowNo) in toInsert)
            {
                ct.ThrowIfCancellationRequested();

                db.Add(obs);
                try
                {
                    await db.SaveChangesAsync(ct);
                    result.Inserted++;
                }
                catch (DbUpdateException ex2) when (IsUniqueViolation(ex2))
                {
                    result.DuplicatesSkipped++;
                    db.ChangeTracker.Clear();
                }
            }

            return result;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // Avoid direct Npgsql reference: detect Postgres unique violation by SqlState == "23505"
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
