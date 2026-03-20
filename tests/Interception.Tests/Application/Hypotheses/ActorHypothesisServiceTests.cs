//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Hypotheses.Dtos;
using Interception.UI.Application.Hypotheses.Services;
using Interception.UI.Domain;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Hypotheses;

public sealed class ActorHypothesisServiceTests
{
    [Fact]
    public async Task CreateAsync_WithSeedParticipants_PersistsCluster_AndReturnsMembersAndObservations()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid seedParticipantId;
        DateTime observedAt = new(2026, 3, 20, 10, 15, 0, DateTimeKind.Utc);

        using (var db = factory.CreateDbContext())
        {
            var observation = Observation.Create(
                observedAt,
                "Радіообмін",
                layer: "L1",
                rmRaw: "RM-01",
                locationRaw: "Лісосмуга",
                districtRaw: "Південь",
                subdivisionRaw: "656 мсп",
                note: "контакт по рації");

            seedParticipantId = observation.AddParticipant("НВ 1", true, "водій").Id;
            observation.AddParticipant("Клим", false, "командир");

            db.Observations.Add(observation);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new ActorHypothesisService(factory);

        var created = await sut.CreateAsync(
            new ActorHypothesisCreateDto("  Гіпотеза водія  ", "  перший аналіз  ", [seedParticipantId]),
            "analyst",
            CancellationToken.None);

        Assert.True(created.IsCreated);
        Assert.NotEqual(Guid.Empty, created.Id);

        var details = await sut.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.NotNull(details);
        Assert.Equal("Гіпотеза водія", details!.Title);
        Assert.Equal("перший аналіз", details.Note);
        Assert.False(details.IsArchived);
        Assert.Equal("analyst", details.CreatedBy);

        var member = Assert.Single(details.Members);
        Assert.Equal(seedParticipantId, member.ObservationParticipantId);
        Assert.True(member.IsUnknown);
        Assert.True(member.StartedAsUnknown);
        Assert.Equal("водій", member.RoleRaw);
        Assert.Equal("НВ 1", member.DisplayLabel);

        var observationRow = Assert.Single(details.Observations);
        Assert.Equal("Радіообмін", observationRow.ActionRaw);
        Assert.Equal("L1", observationRow.Layer);
        Assert.Equal("RM-01", observationRow.RmRaw);
        Assert.Equal("656 мсп", observationRow.SubdivisionRaw);

        var page = await sut.SearchAsync(
            new ActorHypothesisFilterDto { IncludeArchived = false, OnlyResolved = false, Skip = 0, Take = 20 },
            CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal(created.Id, item.Id);
        Assert.Equal(1, item.MembersCount);
        Assert.Equal(observedAt, item.LastSeenAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_RejectsDuplicateNormalizedTitle_AndUpdatesMetadata()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid firstId;
        Guid secondId;

        using (var db = factory.CreateDbContext())
        {
            var first = UnknownCluster.Create("Гіпотеза А", "seed", "seed");
            var second = UnknownCluster.Create("Гіпотеза Б", "старий опис", "seed");
            firstId = first.Id;
            secondId = second.Id;

            db.UnknownClusters.AddRange(first, second);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new ActorHypothesisService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAsync(
            secondId,
            new ActorHypothesisUpdateDto("  гіпотеза а  ", "dup"),
            CancellationToken.None));

        await sut.UpdateAsync(
            secondId,
            new ActorHypothesisUpdateDto("  Гіпотеза Б уточнена  ", "  оновлений опис  "),
            CancellationToken.None);

        var details = await sut.GetByIdAsync(secondId, CancellationToken.None);
        Assert.NotNull(details);
        Assert.Equal("Гіпотеза Б уточнена", details!.Title);
        Assert.Equal("оновлений опис", details.Note);
        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public async Task AddMoveRemoveParticipant_EnforcesMembershipRules_AndUpdatesLinks()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid sourceClusterId;
        Guid targetClusterId;
        Guid firstParticipantId;
        Guid secondParticipantId;

        using (var db = factory.CreateDbContext())
        {
            var observationA = Observation.Create(new DateTime(2026, 3, 21, 8, 0, 0, DateTimeKind.Utc), "Дія A");
            firstParticipantId = observationA.AddParticipant("НВ 1", true, "навідник").Id;

            var observationB = Observation.Create(new DateTime(2026, 3, 21, 9, 0, 0, DateTimeKind.Utc), "Дія B");
            secondParticipantId = observationB.AddParticipant("НВ 2", true, "водій").Id;

            var source = UnknownCluster.Create("Кластер 1", null, "seed");
            source.AddMember(firstParticipantId, "початковий");
            var target = UnknownCluster.Create("Кластер 2", null, "seed");
            sourceClusterId = source.Id;
            targetClusterId = target.Id;

            db.Observations.AddRange(observationA, observationB);
            db.UnknownClusters.AddRange(source, target);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new ActorHypothesisService(factory);

        await sut.AddParticipantAsync(sourceClusterId, secondParticipantId, "manual link", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AddParticipantAsync(
            targetClusterId,
            secondParticipantId,
            "dup",
            CancellationToken.None));

        await sut.MoveParticipantAsync(sourceClusterId, secondParticipantId, targetClusterId, CancellationToken.None);

        using (var db = factory.CreateDbContext())
        {
            Assert.True(await db.UnknownClusterMembers.AnyAsync(x => x.UnknownClusterId == sourceClusterId && x.ObservationParticipantId == firstParticipantId, cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(await db.UnknownClusterMembers.AnyAsync(x => x.UnknownClusterId == sourceClusterId && x.ObservationParticipantId == secondParticipantId, cancellationToken: TestContext.Current.CancellationToken));
            Assert.True(await db.UnknownClusterMembers.AnyAsync(x => x.UnknownClusterId == targetClusterId && x.ObservationParticipantId == secondParticipantId, cancellationToken: TestContext.Current.CancellationToken));
        }

        await sut.RemoveParticipantAsync(targetClusterId, secondParticipantId, CancellationToken.None);

        using (var db = factory.CreateDbContext())
        {
            Assert.False(await db.UnknownClusterMembers.AnyAsync(x => x.UnknownClusterId == targetClusterId && x.ObservationParticipantId == secondParticipantId, cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task MergeAsync_ArchivesSource_AndMovesOnlyUniqueMembers()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid sourceClusterId;
        Guid targetClusterId;
        Guid participantOneId;
        Guid participantTwoId;
        Guid participantThreeId;

        using (var db = factory.CreateDbContext())
        {
            var observation1 = Observation.Create(new DateTime(2026, 3, 22, 7, 0, 0, DateTimeKind.Utc), "Дія 1");
            participantOneId = observation1.AddParticipant("НВ 1", true).Id;

            var observation2 = Observation.Create(new DateTime(2026, 3, 22, 8, 0, 0, DateTimeKind.Utc), "Дія 2");
            participantTwoId = observation2.AddParticipant("НВ 2", true).Id;

            var observation3 = Observation.Create(new DateTime(2026, 3, 22, 9, 0, 0, DateTimeKind.Utc), "Дія 3");
            participantThreeId = observation3.AddParticipant("НВ 3", true).Id;

            var source = UnknownCluster.Create("Source", null, "seed");
            source.AddMember(participantOneId);
            source.AddMember(participantTwoId);

            var target = UnknownCluster.Create("Target", null, "seed");
            target.AddMember(participantTwoId);
            target.AddMember(participantThreeId);

            sourceClusterId = source.Id;
            targetClusterId = target.Id;

            db.Observations.AddRange(observation1, observation2, observation3);
            db.UnknownClusters.AddRange(source, target);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new ActorHypothesisService(factory);
        await sut.MergeAsync(sourceClusterId, targetClusterId, CancellationToken.None);

        using var dbAssert = factory.CreateDbContext();
        var sourceCluster = await dbAssert.UnknownClusters.SingleAsync(x => x.Id == sourceClusterId, cancellationToken: TestContext.Current.CancellationToken);
        var targetMembers = await dbAssert.UnknownClusterMembers
            .Where(x => x.UnknownClusterId == targetClusterId)
            .Select(x => x.ObservationParticipantId)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(sourceCluster.ArchivedAtUtc);
        Assert.Equal(3, targetMembers.Distinct().Count());
        Assert.Contains(participantOneId, targetMembers);
        Assert.Contains(participantTwoId, targetMembers);
        Assert.Contains(participantThreeId, targetMembers);
        Assert.Equal(1, targetMembers.Count(x => x == participantTwoId));

        var openClusters = await sut.SearchOpenClustersAsync(null, 10, CancellationToken.None);
        var onlyOpen = Assert.Single(openClusters);
        Assert.Equal(targetClusterId, onlyOpen.Id);
    }

    [Fact]
    public async Task ResolveAsync_ReusesExistingActor_AndReopenArchiveAffectOpenSearch()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid firstClusterId;
        Guid secondClusterId;

        using (var db = factory.CreateDbContext())
        {
            var observation1 = Observation.Create(new DateTime(2026, 3, 23, 10, 0, 0, DateTimeKind.Utc), "Дія 1");
            var participant1 = observation1.AddParticipant("НВ 1", true, "спостерігач").Id;

            var observation2 = Observation.Create(new DateTime(2026, 3, 23, 11, 0, 0, DateTimeKind.Utc), "Дія 2");
            var participant2 = observation2.AddParticipant("НВ 2", true, "водій").Id;

            var first = UnknownCluster.Create("Кластер А", null, "seed");
            first.AddMember(participant1);
            var second = UnknownCluster.Create("Кластер Б", null, "seed");
            second.AddMember(participant2);

            firstClusterId = first.Id;
            secondClusterId = second.Id;

            db.Observations.AddRange(observation1, observation2);
            db.UnknownClusters.AddRange(first, second);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new ActorHypothesisService(factory);

        await sut.ResolveAsync(firstClusterId, new ResolvedActorUpsertDto("  Петро  ", "  командир  ", "  первинно  "), "analyst", CancellationToken.None);
        await sut.ResolveAsync(secondClusterId, new ResolvedActorUpsertDto("петро", "розвідник", "оновлено"), "analyst", CancellationToken.None);

        using (var db = factory.CreateDbContext())
        {
            var actors = await db.ResolvedActors.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Single(actors);
            Assert.Equal("Петро", actors[0].DisplayName);
            Assert.Equal("розвідник", actors[0].PrimaryRole);
            Assert.Equal("оновлено", actors[0].Note);

            var clusters = await db.UnknownClusters.OrderBy(x => x.Title).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.All(clusters, x =>
            {
                Assert.NotNull(x.ResolvedActorId);
                Assert.NotNull(x.ArchivedAtUtc);
            });
        }

        var resolvedPage = await sut.SearchAsync(
            new ActorHypothesisFilterDto { IncludeArchived = true, OnlyResolved = true, Skip = 0, Take = 20 },
            CancellationToken.None);
        Assert.Equal(2, resolvedPage.TotalCount);
        Assert.All(resolvedPage.Items, x => Assert.Equal("Петро", x.ResolvedActorDisplayName));

        await sut.ReopenAsync(firstClusterId, CancellationToken.None);

        var openAfterReopen = await sut.SearchOpenClustersAsync(null, 10, CancellationToken.None);
        var reopened = Assert.Single(openAfterReopen);
        Assert.Equal(firstClusterId, reopened.Id);
        Assert.False(reopened.IsResolved);

        await sut.ArchiveAsync(firstClusterId, CancellationToken.None);

        var openAfterArchive = await sut.SearchOpenClustersAsync(null, 10, CancellationToken.None);
        Assert.Empty(openAfterArchive);
    }
}
