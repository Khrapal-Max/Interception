//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Import;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;

namespace Interception.Tests.Application.Observations.Import;

public sealed class ObservationImportServiceTests
{
    [Fact]
    public async Task ImportAsync_SkipsInvalidRowsWithEmptyAction()
    {
        var factory = TestDbFactory.CreateFactory();
        var svc = new ObservationImportService(factory);

        var rows = new[]
        {
            new ObservationImportRow(
                new DateOnly(2026, 3, 1),
                (short)DayPart.FirstHalf,
                ActionRaw: "",
                Layer: null,
                RmRaw: null,
                PointRaw: null,
                LocationRaw: null,
                DistrictRaw: null,
                CompanyRaw: null,
                Note: null,
                Participants: [])
        };

        var result = await svc.ImportAsync(rows, new ObservationImportOptions { FirstSourceRowNumber = 10 }, CancellationToken.None);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.Inserted);
        Assert.Equal(1, result.InvalidSkipped);
        Assert.Single(result.Errors);
        Assert.Equal(10, result.Errors[0].RowNumber);
    }

    [Fact]
    public async Task ImportAsync_DeduplicatesInsideBatch_ByContentHash()
    {
        var factory = TestDbFactory.CreateFactory();
        var svc = new ObservationImportService(factory);

        var participants = new[]
        {
            new ObservationImportParticipant("КЛИМ", false, null),
            new ObservationImportParticipant("НВ", true, null),
        };

        var rows = new[]
        {
            new ObservationImportRow(new DateOnly(2026, 3, 1), (short)DayPart.FirstHalf, "Перевезення", "136.2550", "фірма A", "коорд", "Степове", "Р1", null, null, participants),
            new ObservationImportRow(new DateOnly(2026, 3, 1), (short)DayPart.FirstHalf, "Перевезення", "136.2550", "фірма A", "коорд", "Степове", "Р1", null, null, participants),
        };

        var result = await svc.ImportAsync(rows, new ObservationImportOptions(), CancellationToken.None);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.DuplicatesSkipped);

        await using var verify = factory.CreateDbContext();
        Assert.Equal(1, verify.Observations.Count());
        Assert.Equal(2, verify.ObservationParticipants.Count());
    }

    [Fact]
    public async Task ImportAsync_DeduplicatesAgainstDatabase_ByContentHash()
    {
        var factory = TestDbFactory.CreateFactory();

        // seed existing observation
        await using (var seed = factory.CreateDbContext())
        {
            var existing = Observation.Create(new DateOnly(2026, 3, 1), DayPart.FirstHalf, "Перевезення", layer: "1.1", locationRaw: "Степове");
            existing.AddParticipant("КЛИМ", false, ordinal: 1);
            seed.Add(existing);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        var svc = new ObservationImportService(factory);

        var rows = new[]
        {
            new ObservationImportRow(
                new DateOnly(2026, 3, 1),
                (short)DayPart.FirstHalf,
                "Перевезення",
                "1.1",
                null,
                null,
                "Степове",
                null,
                null,
                null,
                [new ObservationImportParticipant("КЛИМ", false, null)])
        };

        var result = await svc.ImportAsync(rows, new ObservationImportOptions { DeduplicateByHash = true }, CancellationToken.None);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.Inserted);
        Assert.Equal(1, result.DuplicatesSkipped);
        Assert.Equal(0, result.InvalidSkipped);

        await using var verify = factory.CreateDbContext();
        Assert.Equal(1, verify.Observations.Count());
    }

    [Fact]
    public async Task ImportAsync_WhenDeduplicateByHashFalse_DoesNotCheckDb()
    {
        var factory = TestDbFactory.CreateFactory();

        // seed existing observation
        await using (var seed = factory.CreateDbContext())
        {
            var existing = Observation.Create(new DateOnly(2026, 3, 1), DayPart.FirstHalf, "Перевезення", layer: "1.1", locationRaw: "Степове");
            existing.AddParticipant("КЛИМ", false, ordinal: 1);
            seed.Add(existing);
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        var svc = new ObservationImportService(factory);

        var rows = new[]
        {
            new ObservationImportRow(
                new DateOnly(2026, 3, 1),
                (short)DayPart.FirstHalf,
                "Перевезення",
                "1.1",
                null,
                null,
                "Степове",
                null,
                null,
                null,
                [new ObservationImportParticipant("КЛИМ", false, null)])
        };

        var result = await svc.ImportAsync(rows, new ObservationImportOptions { DeduplicateByHash = false }, CancellationToken.None);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(0, result.DuplicatesSkipped);
    }

    [Fact]
    public async Task ImportAsync_MarksRowInvalid_WhenParticipantsContainDuplicateKnownLabels()
    {
        var factory = TestDbFactory.CreateFactory();
        var svc = new ObservationImportService(factory);

        var rows = new[]
        {
            new ObservationImportRow(
                new DateOnly(2026, 3, 1),
                (short)DayPart.FirstHalf,
                "Перевезення",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [
                    new ObservationImportParticipant("КЛИМ", false, null),
                    new ObservationImportParticipant("  клим  ", false, null),
                ])
        };

        var result = await svc.ImportAsync(rows, new ObservationImportOptions { FirstSourceRowNumber = 2 }, CancellationToken.None);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.Inserted);
        Assert.Equal(1, result.InvalidSkipped);
        Assert.Single(result.Errors);
        Assert.Contains("already exists", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] value =
        [
            "час;особа 1;особа 2;локація;район;дія",
            "04.03.2026 AM;КЛИМ;НВ;Степове;Р1;Перевезення"
        ];

    [Fact]
    public async Task ImportCsvAsync_ParsesAndImports()
    {
        var factory = TestDbFactory.CreateFactory();
        var svc = new ObservationImportService(factory);

        // Semicolon CSV (common for UA locale)
        var csv = string.Join("\n", value);

        await using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = await svc.ImportCsvAsync(ms, new ObservationImportOptions { FirstSourceRowNumber = 2 }, CancellationToken.None);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(0, result.InvalidSkipped);
        Assert.Equal(0, result.DuplicatesSkipped);

        await using var verify = factory.CreateDbContext();
        var obs = verify.Observations.Single();
        Assert.Equal(new DateOnly(2026, 3, 4), obs.ObservedDate);
        Assert.Equal(DayPart.FirstHalf, obs.DayPart);
        Assert.Equal("Перевезення", obs.ActionRaw);
        Assert.Equal("Степове", obs.LocationRaw);

        Assert.Equal(2, verify.ObservationParticipants.Count());
    }
}
