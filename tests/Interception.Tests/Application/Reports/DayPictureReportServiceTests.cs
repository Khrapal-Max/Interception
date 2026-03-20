//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Reports.Dtos;
using Interception.UI.Application.Reports.Services;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;

namespace Interception.Tests.Application.Reports;

public sealed class DayPictureReportServiceTests
{
    [Fact]
    public async Task BuildAsync_BuildsLogicalThread_WithSharedSignalsAndStrongLinkReasons()
    {
        var factory = TestDbFactory.CreateFactory();
        var day = new DateOnly(2026, 3, 29);
        Guid firstObservationId;
        Guid secondObservationId;

        using (var db = factory.CreateDbContext())
        {
            var moveAction = ObservationAction.Create("Рух");
            var resolvedActor = ResolvedActor.Create("Клим", "водій", createdBy: "seed");
            var resolvedSubdivision = ResolvedSubdivision.Create("656 мсп", "L1", "RM-1", createdBy: "seed");

            var first = Observation.Create(
                day.ToDateTime(new TimeOnly(8, 0)),
                "Вихід колони",
                layer: "L1",
                rmRaw: "RM-1",
                locationRaw: "Лісосмуга",
                districtRaw: "Південь",
                note: "перший контакт");
            first.BindAction(moveAction.Id);
            var firstParticipant = first.AddParticipant("НВ 1", true, "водій", 1);
            first.AddTag("Клим", TagKind.Person);
            first.AddProbableAction(moveAction.Id, 0.82m, "rule", ProbableActionSource.Rule);

            var second = Observation.Create(
                day.ToDateTime(new TimeOnly(8, 18)),
                "Рух до рубежу",
                layer: "L1",
                rmRaw: "RM-1",
                locationRaw: "Лісосмуга",
                districtRaw: "Південь",
                note: "другий контакт");
            second.BindAction(moveAction.Id);
            var secondParticipant = second.AddParticipant("НВ 2", true, "водій", 1);
            second.AddTag("Клим", TagKind.Person);
            second.AddProbableAction(moveAction.Id, 0.74m, "manual", ProbableActionSource.Manual);

            var third = Observation.Create(
                day.ToDateTime(new TimeOnly(12, 30)),
                "Обстріл позиції",
                layer: "L9",
                rmRaw: "RM-9",
                locationRaw: "Поле",
                districtRaw: "Північ",
                note: "непов'язаний епізод");
            third.AddParticipant("Інший", false, "навідник", 1);

            var actorCluster = UnknownCluster.Create("Гіпотеза Клим", "зв'язок по голосу", "seed");
            actorCluster.AddMember(firstParticipant.Id, "obs-1");
            actorCluster.AddMember(secondParticipant.Id, "obs-2");
            actorCluster.ResolveToActor(resolvedActor.Id);

            var subdivisionCluster = UnknownSubdivisionCluster.Create("невід. 656", "L1", "RM-1", "seed", "seed");
            subdivisionCluster.AddObservation(first.Id, "obs-1");
            subdivisionCluster.AddObservation(second.Id, "obs-2");
            subdivisionCluster.Resolve(resolvedSubdivision.Id);

            db.ObservationActions.Add(moveAction);
            db.ResolvedActors.Add(resolvedActor);
            db.ResolvedSubdivisions.Add(resolvedSubdivision);
            db.Observations.AddRange(first, second, third);
            db.UnknownClusters.Add(actorCluster);
            db.UnknownSubdivisionClusters.Add(subdivisionCluster);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            firstObservationId = first.Id;
            secondObservationId = second.Id;
        }

        var sut = new DayPictureReportService(factory);
        var report = await sut.BuildAsync(new DayPictureFilterDto(day, MinLinkScore: 3), CancellationToken.None);

        Assert.Equal(3, report.TotalObservations);
        Assert.Equal(2, report.TotalThreads);

        var linkedThread = Assert.Single(report.Threads, x => x.Observations.Count == 2);
        Assert.Contains("2 под.", linkedThread.Headline);
        Assert.Equal(new[] { firstObservationId, secondObservationId }, linkedThread.Observations.Select(x => x.Id).ToArray());

        var link = Assert.Single(linkedThread.Links);
        Assert.True(link.Score >= 10);
        Assert.Contains(link.Reasons, x => x.StartsWith("спільний підрозділ:", StringComparison.Ordinal));
        Assert.Contains(link.Reasons, x => x.StartsWith("спільна встановлена особа:", StringComparison.Ordinal));
        Assert.Contains(link.Reasons, x => x == "спільний тип дії");

        Assert.Contains(linkedThread.SharedSignals, x => x.Kind == "subdivision" && x.Value == "656 мсп" && x.Count == 2);
        Assert.Contains(linkedThread.SharedSignals, x => x.Kind == "person" && x.Value == "Клим" && x.Count == 2);

        Assert.All(linkedThread.Observations, x =>
        {
            Assert.Equal("656 мсп", x.EffectiveSubdivision);
            Assert.True(x.SubdivisionResolved);
            Assert.Contains("Клим", x.People);
            Assert.Contains("Клим", x.Tags);
            Assert.Contains("Рух", x.ProbableActions);
        });
    }

    [Fact]
    public async Task BuildAsync_AppliesLayerRmAndQueryFilters()
    {
        var factory = TestDbFactory.CreateFactory();
        var day = new DateOnly(2026, 3, 30);

        using (var db = factory.CreateDbContext())
        {
            var first = Observation.Create(
                day.ToDateTime(new TimeOnly(6, 40)),
                "Пошук цілі",
                layer: "L-1",
                rmRaw: "RM-1",
                locationRaw: "Ліс",
                districtRaw: "Схід",
                note: "ціль у лісі");
            first.AddParticipant("Клим", false, "оператор", 1);

            var second = Observation.Create(
                day.ToDateTime(new TimeOnly(7, 20)),
                "Пошук цілі",
                layer: "L-2",
                rmRaw: "RM-2",
                locationRaw: "Поле",
                districtRaw: "Захід",
                note: "ціль у полі");
            second.AddParticipant("Шторм", false, "оператор", 1);

            db.Observations.AddRange(first, second);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new DayPictureReportService(factory);

        var report = await sut.BuildAsync(
            new DayPictureFilterDto(day, Layer: "L-1", RmRaw: "RM-1", Query: "ліс", MinLinkScore: 2),
            CancellationToken.None);

        Assert.Equal(1, report.TotalObservations);
        Assert.Equal(1, report.TotalThreads);

        var thread = Assert.Single(report.Threads);
        var observation = Assert.Single(thread.Observations);
        Assert.Equal("L-1", observation.Layer);
        Assert.Equal("RM-1", observation.RmRaw);
        Assert.Equal("Ліс", observation.LocationRaw);
        Assert.Empty(thread.Links);
    }
}
