//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Hypotheses.Dtos;
using Interception.UI.Application.Hypotheses.Services;
using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Hypotheses;

public sealed class SubdivisionHypothesisServiceTests
{
    [Fact]
    public async Task CreateAsync_WithSeedObservation_PersistsCluster_AndReturnsDetails()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid observationId;
        DateTime observedAt = new(2026, 3, 24, 9, 30, 0, DateTimeKind.Utc);

        using (var db = factory.CreateDbContext())
        {
            var observation = Observation.Create(
                observedAt,
                "Переміщення",
                layer: "L-3",
                rmRaw: "RM-33",
                locationRaw: "Посадка",
                districtRaw: "Схід",
                subdivisionRaw: "невідомий 3 мсб",
                note: "помічено на маршруті");

            observationId = observation.Id;
            db.Observations.Add(observation);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new SubdivisionHypothesisService(factory);

        var created = await sut.CreateAsync(
            new SubdivisionHypothesisCreateDto("  3 мсб ?  ", "  L-3  ", "  RM-33  ", "  перший аналіз  ", [observationId]),
            "analyst",
            CancellationToken.None);

        Assert.True(created.IsCreated);

        var details = await sut.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.NotNull(details);
        Assert.Equal("3 мсб ?", details!.LabelRaw);
        Assert.Equal("L-3", details.LayerHint);
        Assert.Equal("RM-33", details.RmHint);
        Assert.Equal("перший аналіз", details.Note);
        Assert.Equal("analyst", details.CreatedBy);
        Assert.False(details.IsArchived);

        var observationRow = Assert.Single(details.Observations);
        Assert.Equal(observationId, observationRow.ObservationId);
        Assert.Equal("Переміщення", observationRow.ActionRaw);
        Assert.Equal("невідомий 3 мсб", observationRow.SubdivisionRaw);

        var page = await sut.SearchAsync(
            new SubdivisionHypothesisFilterDto { IncludeArchived = false, OnlyResolved = false, Skip = 0, Take = 20 },
            CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal(created.Id, item.Id);
        Assert.Equal(1, item.ObservationsCount);
        Assert.Equal(observedAt, item.LastSeenAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_RejectsDuplicateNormalizedLabel_AndUpdatesHintsAndNote()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid firstId;
        Guid secondId;

        using (var db = factory.CreateDbContext())
        {
            var first = UnknownSubdivisionCluster.Create("1 мсб", "A", "RM-1", "seed", "seed");
            var second = UnknownSubdivisionCluster.Create("2 мсб", "B", "RM-2", "старе", "seed");
            firstId = first.Id;
            secondId = second.Id;

            db.UnknownSubdivisionClusters.AddRange(first, second);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new SubdivisionHypothesisService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAsync(
            secondId,
            new SubdivisionHypothesisUpdateDto("  1 МСБ  ", "X", "Y", "dup"),
            CancellationToken.None));

        await sut.UpdateAsync(
            secondId,
            new SubdivisionHypothesisUpdateDto("2 мсб", "  C  ", "  RM-9  ", "  оновлено  "),
            CancellationToken.None);

        var details = await sut.GetByIdAsync(secondId, CancellationToken.None);
        Assert.NotNull(details);
        Assert.Equal("2 мсб", details!.LabelRaw);
        Assert.Equal("C", details.LayerHint);
        Assert.Equal("RM-9", details.RmHint);
        Assert.Equal("оновлено", details.Note);
        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public async Task AddMoveRemoveObservation_EnforcesRules_AndUpdatesLinks()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid sourceClusterId;
        Guid targetClusterId;
        Guid firstObservationId;
        Guid secondObservationId;

        using (var db = factory.CreateDbContext())
        {
            var observation1 = Observation.Create(new DateTime(2026, 3, 25, 8, 0, 0, DateTimeKind.Utc), "Дія 1", subdivisionRaw: "невідомий 1 мсб");
            var observation2 = Observation.Create(new DateTime(2026, 3, 25, 9, 0, 0, DateTimeKind.Utc), "Дія 2", subdivisionRaw: "невідомий 2 мсб");
            firstObservationId = observation1.Id;
            secondObservationId = observation2.Id;

            var source = UnknownSubdivisionCluster.Create("Гіпотеза А", "L1", "RM-1", null, "seed");
            source.AddObservation(firstObservationId, "початковий");
            var target = UnknownSubdivisionCluster.Create("Гіпотеза Б", "L2", "RM-2", null, "seed");
            sourceClusterId = source.Id;
            targetClusterId = target.Id;

            db.Observations.AddRange(observation1, observation2);
            db.UnknownSubdivisionClusters.AddRange(source, target);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new SubdivisionHypothesisService(factory);

        await sut.AddObservationAsync(sourceClusterId, secondObservationId, "manual", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AddObservationAsync(
            targetClusterId,
            secondObservationId,
            "dup",
            CancellationToken.None));

        await sut.MoveObservationAsync(sourceClusterId, secondObservationId, targetClusterId, CancellationToken.None);

        using (var db = factory.CreateDbContext())
        {
            Assert.True(await db.UnknownSubdivisionObservations.AnyAsync(x => x.UnknownSubdivisionClusterId == sourceClusterId && x.ObservationId == firstObservationId, cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(await db.UnknownSubdivisionObservations.AnyAsync(x => x.UnknownSubdivisionClusterId == sourceClusterId && x.ObservationId == secondObservationId, cancellationToken: TestContext.Current.CancellationToken));
            Assert.True(await db.UnknownSubdivisionObservations.AnyAsync(x => x.UnknownSubdivisionClusterId == targetClusterId && x.ObservationId == secondObservationId, cancellationToken: TestContext.Current.CancellationToken));
        }

        await sut.RemoveObservationAsync(targetClusterId, secondObservationId, CancellationToken.None);

        using (var db = factory.CreateDbContext())
        {
            Assert.False(await db.UnknownSubdivisionObservations.AnyAsync(x => x.UnknownSubdivisionClusterId == targetClusterId && x.ObservationId == secondObservationId, cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task MergeResolveReopenArchive_ReuseResolvedSubdivision_AndAffectOpenSearch()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid sourceClusterId;
        Guid targetClusterId;
        Guid observationOneId;
        Guid observationTwoId;
        Guid observationThreeId;

        using (var db = factory.CreateDbContext())
        {
            var observation1 = Observation.Create(new DateTime(2026, 3, 26, 7, 0, 0, DateTimeKind.Utc), "Дія 1", subdivisionRaw: "невідомий 3 мсб");
            var observation2 = Observation.Create(new DateTime(2026, 3, 26, 8, 0, 0, DateTimeKind.Utc), "Дія 2", subdivisionRaw: "невідомий 3 мсб");
            var observation3 = Observation.Create(new DateTime(2026, 3, 26, 9, 0, 0, DateTimeKind.Utc), "Дія 3", subdivisionRaw: "невідомий 5 мсб");
            observationOneId = observation1.Id;
            observationTwoId = observation2.Id;
            observationThreeId = observation3.Id;

            var source = UnknownSubdivisionCluster.Create("Джерело", "L1", "RM-1", null, "seed");
            source.AddObservation(observationOneId);
            source.AddObservation(observationTwoId);

            var target = UnknownSubdivisionCluster.Create("Ціль", "L1", "RM-1", null, "seed");
            target.AddObservation(observationTwoId);
            target.AddObservation(observationThreeId);

            sourceClusterId = source.Id;
            targetClusterId = target.Id;

            db.Observations.AddRange(observation1, observation2, observation3);
            db.UnknownSubdivisionClusters.AddRange(source, target);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new SubdivisionHypothesisService(factory);

        await sut.MergeAsync(sourceClusterId, targetClusterId, CancellationToken.None);

        using (var db = factory.CreateDbContext())
        {
            var source = await db.UnknownSubdivisionClusters.SingleAsync(x => x.Id == sourceClusterId, cancellationToken: TestContext.Current.CancellationToken);
            Assert.NotNull(source.ArchivedAtUtc);

            var targetObservationIds = await db.UnknownSubdivisionObservations
                .Where(x => x.UnknownSubdivisionClusterId == targetClusterId)
                .Select(x => x.ObservationId)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(3, targetObservationIds.Distinct().Count());
            Assert.Contains(observationOneId, targetObservationIds);
            Assert.Contains(observationTwoId, targetObservationIds);
            Assert.Contains(observationThreeId, targetObservationIds);
        }

        await sut.ResolveAsync(targetClusterId, new ResolvedSubdivisionUpsertDto(" 656 мсп ", "L-main", "RM-main", "підтверджено"), "analyst", CancellationToken.None);
        await sut.ResolveAsync(sourceClusterId, new ResolvedSubdivisionUpsertDto("656 мсп", "L-updated", "RM-updated", "оновлено"), "analyst", CancellationToken.None);

        using (var db = factory.CreateDbContext())
        {
            var resolved = await db.ResolvedSubdivisions.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Single(resolved);
            Assert.Equal("656 мсп", resolved[0].Name);
            Assert.Equal("L-updated", resolved[0].LayerHint);
            Assert.Equal("RM-updated", resolved[0].RmHint);
            Assert.Equal("оновлено", resolved[0].Note);
        }

        var resolvedPage = await sut.SearchAsync(
            new SubdivisionHypothesisFilterDto { IncludeArchived = true, OnlyResolved = true, Skip = 0, Take = 20 },
            CancellationToken.None);
        Assert.Equal(2, resolvedPage.TotalCount);
        Assert.All(resolvedPage.Items, x => Assert.Equal("656 мсп", x.ResolvedSubdivisionName));

        await sut.ReopenAsync(targetClusterId, CancellationToken.None);

        var openAfterReopen = await sut.SearchOpenClustersAsync(null, 10, CancellationToken.None);
        var reopened = Assert.Single(openAfterReopen);
        Assert.Equal(targetClusterId, reopened.Id);
        Assert.False(reopened.IsResolved);

        await sut.ArchiveAsync(targetClusterId, CancellationToken.None);

        var openAfterArchive = await sut.SearchOpenClustersAsync(null, 10, CancellationToken.None);
        Assert.Empty(openAfterArchive);
    }
}
