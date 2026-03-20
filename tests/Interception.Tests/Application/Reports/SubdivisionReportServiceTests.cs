//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Reports.Dtos;
using Interception.UI.Application.Reports.Services;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;

namespace Interception.Tests.Application.Reports;

public sealed class SubdivisionReportServiceTests
{
    [Fact]
    public async Task BuildAsync_GroupsResolvedRawAndWithoutSubdivision_AndAggregatesStats()
    {
        var factory = TestDbFactory.CreateFactory();
        var day = new DateTime(2026, 3, 31, 0, 0, 0);

        using (var db = factory.CreateDbContext())
        {
            var moveAction = ObservationAction.Create("Рух");
            var resolvedActor = ResolvedActor.Create("Клим", "водій", createdBy: "seed");
            var resolvedSubdivision = ResolvedSubdivision.Create("656 мсп", "L1", "RM-1", createdBy: "seed");

            var first = Observation.Create(
                day.AddHours(7),
                "Вихід",
                layer: "L1",
                rmRaw: "RM-1",
                locationRaw: "Посадка",
                districtRaw: "Південь",
                note: "resolved-1");
            first.BindAction(moveAction.Id);
            var firstParticipant = first.AddParticipant("НВ 1", true, "водій", 1);
            first.AddTag("Клим", TagKind.Person);

            var second = Observation.Create(
                day.AddHours(8),
                "Рух до рубежу",
                layer: "L1",
                rmRaw: "RM-1",
                locationRaw: "Посадка",
                districtRaw: "Південь",
                note: "resolved-2");
            second.BindAction(moveAction.Id);
            var secondParticipant = second.AddParticipant("НВ 2", true, "водій", 1);
            second.AddParticipant("НВ 3", true, "стрілець", 2);
            second.AddTag("Клим", TagKind.Person);

            var raw = Observation.Create(
                day.AddHours(9),
                "Маневр",
                layer: "L2",
                rmRaw: "RM-2",
                locationRaw: "Село",
                districtRaw: "Схід",
                subdivisionRaw: "69 тп",
                note: "raw-group");
            raw.AddParticipant("Мороз", false, "командир", 1);
            raw.AddTag("Село", TagKind.Location);

            var none = Observation.Create(
                day.AddHours(10),
                "Очікування",
                layer: "L3",
                rmRaw: "RM-3",
                locationRaw: "Балка",
                districtRaw: "Захід",
                note: "without-subdivision");
            none.AddParticipant("НВ 4", true, "черговий", 1);

            var actorCluster = UnknownCluster.Create("Гіпотеза Клим", null, "seed");
            actorCluster.AddMember(firstParticipant.Id);
            actorCluster.AddMember(secondParticipant.Id);
            actorCluster.ResolveToActor(resolvedActor.Id);

            var subdivisionCluster = UnknownSubdivisionCluster.Create("невід. 656", "L1", "RM-1", null, "seed");
            subdivisionCluster.AddObservation(first.Id);
            subdivisionCluster.AddObservation(second.Id);
            subdivisionCluster.Resolve(resolvedSubdivision.Id);

            db.ObservationActions.Add(moveAction);
            db.ResolvedActors.Add(resolvedActor);
            db.ResolvedSubdivisions.Add(resolvedSubdivision);
            db.Observations.AddRange(first, second, raw, none);
            db.UnknownClusters.Add(actorCluster);
            db.UnknownSubdivisionClusters.Add(subdivisionCluster);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new SubdivisionReportService(factory);
        var report = await sut.BuildAsync(
            new SubdivisionReportFilterDto(
                ObservedFrom: day,
                ObservedTo: day.AddHours(23).AddMinutes(59),
                IncludeRawUnresolved: true,
                IncludeWithoutSubdivision: true,
                TopActions: 5,
                TopActors: 5,
                TopTags: 5,
                SampleObservations: 5),
            CancellationToken.None);

        Assert.Equal(4, report.TotalObservations);
        Assert.Equal(3, report.TotalGroups);

        var resolvedGroup = Assert.Single(report.Items, x => x.DisplayName == "656 мсп");
        Assert.True(resolvedGroup.IsResolved);
        Assert.Equal("L1", resolvedGroup.LayerHint);
        Assert.Equal("RM-1", resolvedGroup.RmHint);
        Assert.Equal(2, resolvedGroup.ObservationsCount);
        Assert.Equal(2, resolvedGroup.DistinctActorsCount);
        Assert.Equal(3, resolvedGroup.UnknownParticipantsCount);
        Assert.Equal(day.AddHours(7), resolvedGroup.FirstObservedAt);
        Assert.Equal(day.AddHours(8), resolvedGroup.LastObservedAt);
        Assert.Contains(resolvedGroup.Actions, x => x.Action == "Рух" && x.Count == 2 && x.IsCatalogAction);
        Assert.Contains(resolvedGroup.Actors, x => x.DisplayName == "Клим" && x.Count == 2 && x.IsResolved && x.Source == "resolved");
        Assert.Contains(resolvedGroup.Tags, x => x.Value == "Клим" && x.Kind == TagKind.Person && x.Count == 2);
        Assert.Equal(2, resolvedGroup.Samples.Count);

        var rawGroup = Assert.Single(report.Items, x => x.DisplayName == "69 тп");
        Assert.False(rawGroup.IsResolved);
        Assert.Equal(1, rawGroup.ObservationsCount);
        Assert.Contains(rawGroup.Actions, x => x.Action == "Маневр" && x.Count == 1 && !x.IsCatalogAction);
        Assert.Contains(rawGroup.Actors, x => x.DisplayName == "Мороз" && x.Count == 1 && !x.IsResolved && x.Source == "known");
        Assert.Contains(rawGroup.Tags, x => x.Value == "Село" && x.Kind == TagKind.Location && x.Count == 1);

        var noneGroup = Assert.Single(report.Items, x => x.DisplayName == "Без визначеного підрозділу");
        Assert.False(noneGroup.IsResolved);
        Assert.Equal(1, noneGroup.ObservationsCount);
        Assert.Equal(1, noneGroup.UnknownParticipantsCount);
    }

    [Fact]
    public async Task BuildAsync_CanExcludeRawUnresolvedGroups_WhileKeepingFilteredObservationCount()
    {
        var factory = TestDbFactory.CreateFactory();
        var day = new DateTime(2026, 4, 1, 0, 0, 0);

        using (var db = factory.CreateDbContext())
        {
            var raw = Observation.Create(
                day.AddHours(6),
                "Радіообмін",
                layer: "L2",
                rmRaw: "RM-2",
                subdivisionRaw: "70 тп",
                note: "selected record");
            raw.AddParticipant("Клим", false, "оператор", 1);

            var none = Observation.Create(
                day.AddHours(7),
                "Очікування",
                layer: "L3",
                rmRaw: "RM-3",
                note: "selected record");
            none.AddParticipant("НВ 9", true, "черговий", 1);

            db.Observations.AddRange(raw, none);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new SubdivisionReportService(factory);
        var report = await sut.BuildAsync(
            new SubdivisionReportFilterDto(
                ObservedFrom: day,
                ObservedTo: day.AddHours(23).AddMinutes(59),
                Query: "selected",
                IncludeRawUnresolved: false,
                IncludeWithoutSubdivision: true),
            CancellationToken.None);

        Assert.Equal(2, report.TotalObservations);
        Assert.Equal(1, report.TotalGroups);

        var item = Assert.Single(report.Items);
        Assert.Equal("Без визначеного підрозділу", item.DisplayName);
        Assert.Equal(2, item.ObservationsCount);
    }
}
