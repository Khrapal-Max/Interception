//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Dtos.Import;
using Interception.UI.Application.Observations.Services;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application;

public sealed class ObservationImportServiceTests
{
    [Fact]
    public async Task ImportAsync_ImportsNewRows_AndCountsBatchDbDuplicatesAndErrors()
    {
        var factory = TestDbFactory.CreateFactory();
        var existingDate = new DateTime(2026, 3, 25, 6, 30, 0);

        using (var dbTest = factory.CreateDbContext())
        {
            var existing = Observation.Create(
                existingDate,
                "Доповідь",
                layer: "A-1",
                rmRaw: "RM-1",
                locationRaw: "Посадка",
                districtRaw: "Район 1",
                subdivisionRaw: "656 мсп",
                subdivisionStrength: SubdivisionLinkStrength.Medium,
                subdivisionSource: ObservationSubdivisionSource.Import,
                note: "старий рядок",
                source: "xlsx",
                sourceRow: 1,
                createdBy: "seed");

            existing.AddParticipant("НВ 1 мсб", true, "водій", 1);
            dbTest.Observations.Add(existing);
            await dbTest.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var rows = new List<ObservationImportRowDto>
        {
            new()
            {
                RowNumber = 1,
                ObservedDate = existingDate,
                ActionRaw = "Доповідь",
                Layer = "A-1",
                RmRaw = "RM-1",
                LocationRaw = "Посадка",
                DistrictRaw = "Район 1",
                SubdivisionRaw = "656 мсп",
                SubdivisionStrength = SubdivisionLinkStrength.Medium,
                SubdivisionSource = ObservationSubdivisionSource.Import,
                Note = "старий рядок",
                Participants =
                [
                    new ObservationImportParticipantDto { LabelRaw = "НВ 9 інший ярлик", RoleRaw = "водій" }
                ]
            },
            new()
            {
                RowNumber = 2,
                ObservedDate = existingDate,
                ActionRaw = "Доповідь",
                Layer = "A-1",
                RmRaw = "RM-1",
                LocationRaw = "Посадка",
                DistrictRaw = "Район 1",
                SubdivisionRaw = "656 мсп",
                SubdivisionStrength = SubdivisionLinkStrength.Medium,
                SubdivisionSource = ObservationSubdivisionSource.Import,
                Note = "старий рядок",
                Participants =
                [
                    new ObservationImportParticipantDto { LabelRaw = "НВ 77 ще інший", RoleRaw = "водій" }
                ]
            },
            new()
            {
                RowNumber = 3,
                ObservedDate = new DateTime(2026, 3, 25, 7, 15, 0),
                ActionRaw = "Рух колони",
                Layer = "B-2",
                RmRaw = "RM-2",
                LocationRaw = "Галявина",
                DistrictRaw = "Район 2",
                SubdivisionRaw = "69 обрп",
                SubdivisionStrength = SubdivisionLinkStrength.Strong,
                SubdivisionSource = ObservationSubdivisionSource.Import,
                Note = "новий рядок",
                Participants =
                [
                    new ObservationImportParticipantDto { LabelRaw = "НВ із складу 656 мсп", RoleRaw = "навідник" },
                    new ObservationImportParticipantDto { LabelRaw = "  Клим  ", RoleRaw = "  водій  " }
                ]
            },
            new()
            {
                RowNumber = 4,
                ObservedDate = new DateTime(2026, 3, 25, 8, 0, 0),
                ActionRaw = "   "
            }
        };

        var sut = new ObservationImportService(factory);
        var result = await sut.ImportAsync(rows, "xlsx", Guid.NewGuid(), "import-user", CancellationToken.None);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(2, result.DuplicateCount);
        Assert.Equal(1, result.ErrorCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(4, error.RowNumber);
        Assert.Contains("Action is required", error.Message, StringComparison.OrdinalIgnoreCase);
        using var db = factory.CreateDbContext();
        Assert.Equal(2, await db.Observations.CountAsync(cancellationToken: TestContext.Current.CancellationToken));

        var imported = await db.Observations
            .Include(x => x.Participants)
            .SingleAsync(x => x.ActionRaw == "Рух колони", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("B-2", imported.Layer);
        Assert.Equal("RM-2", imported.RmRaw);
        Assert.Equal("69 обрп", imported.SubdivisionRaw);
        Assert.Equal(SubdivisionLinkStrength.Strong, imported.SubdivisionStrength);
        Assert.Equal(ObservationSubdivisionSource.Import, imported.SubdivisionSource);
        Assert.Equal("xlsx", imported.Source);
        Assert.Equal(3, imported.SourceRow);
        Assert.Equal("import-user", imported.CreatedBy);

        Assert.Collection(
            imported.Participants.OrderBy(x => x.Ordinal),
            unknown =>
            {
                Assert.Equal("НВ із складу 656 мсп", unknown.LabelRaw);
                Assert.True(unknown.IsUnknown);
                Assert.True(unknown.StartedAsUnknown);
                Assert.Equal("навідник", unknown.RoleRaw);
            },
            known =>
            {
                Assert.Equal("Клим", known.LabelRaw);
                Assert.False(known.IsUnknown);
                Assert.False(known.StartedAsUnknown);
                Assert.Equal("водій", known.RoleRaw);
            });
    }
}
