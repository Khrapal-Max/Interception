//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Analytics.Events;
using Interception.UI.Application.Common.Events;
using Interception.UI.Application.Analytics.Services;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Domain.Records;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Interception.Tests.Application.Analytics;

public sealed class ParticipantCandidateGroupCommandServiceTests
{
    private static ParticipantCandidateGroupCommandService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task ConfirmAsync_CreatesResolvedParticipant_AndMarksGroupAsConfirmed()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        var group = ParticipantCandidateGroup.Create(
            [new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1), new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 2)],
            0.79,
            new PatternMatchReasons { SameFrequency = true });

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        await service.ConfirmAsync(group.Id, "  ШАПКА  ", "  analyst  ", "  оператор  ", "  1 мсб  ", ct);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var persistedGroup = await verifyDb.ParticipantCandidateGroups.SingleAsync(ct);
        var resolved = await verifyDb.ResolvedParticipants.SingleAsync(ct);

        resolved.Name.Should().Be("ШАПКА");
        resolved.Role.Should().Be("оператор");
        resolved.Division.Should().Be("1 мсб");
        resolved.ConfirmedBy.Should().Be("analyst");

        persistedGroup.Status.Should().Be(CandidateGroupStatus.Confirmed);
        persistedGroup.SuggestedName.Should().Be("ШАПКА");
        persistedGroup.ResolvedBy.Should().Be("analyst");
        persistedGroup.ResolvedAt.Should().NotBeNull();
        persistedGroup.ResolvedParticipantId.Should().Be(resolved.Id);
    }

    [Fact]
    public async Task ConfirmAsync_WhenSingleContextFreeResolvedParticipantExists_EnrichesItWithoutCreatingDuplicate()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        var existingResolved = ResolvedParticipant.Create("ГРОМ", "seed", null, null);
        var group = ParticipantCandidateGroup.Create(
            [new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1), new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 2)],
            0.82,
            new PatternMatchReasons { SameVector = true });

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ResolvedParticipants.Add(existingResolved);
            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        await service.ConfirmAsync(group.Id, "ГРОМ", "analyst", "нова роль", "новий підрозділ", ct);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        (await verifyDb.ResolvedParticipants.CountAsync(ct)).Should().Be(1);

        var persistedResolved = await verifyDb.ResolvedParticipants.SingleAsync(ct);
        persistedResolved.Id.Should().Be(existingResolved.Id);
        persistedResolved.Role.Should().Be("нова роль");
        persistedResolved.Division.Should().Be("новий підрозділ");

        var persistedGroup = await verifyDb.ParticipantCandidateGroups.SingleAsync(ct);
        persistedGroup.Status.Should().Be(CandidateGroupStatus.Confirmed);
        persistedGroup.ResolvedParticipantId.Should().Be(existingResolved.Id);
    }

    [Fact]
    public async Task ConfirmAsync_WhenOnlyNameMatchesButContextDiffers_CreatesAdditionalResolvedParticipant()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        var existingResolved = ResolvedParticipant.Create("ГРОМ", "seed", "стара роль", "старий підрозділ");
        var group = ParticipantCandidateGroup.Create(
            [new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1), new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 2)],
            0.82,
            new PatternMatchReasons { SameVector = true });

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ResolvedParticipants.Add(existingResolved);
            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        await service.ConfirmAsync(group.Id, "ГРОМ", "analyst", "нова роль", "новий підрозділ", ct);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        (await verifyDb.ResolvedParticipants.CountAsync(ct)).Should().Be(2);
        verifyDb.ResolvedParticipants.Should().Contain(x => x.Id == existingResolved.Id);
        verifyDb.ResolvedParticipants.Should().Contain(x =>
            x.Name == "ГРОМ" &&
            x.Role == "нова роль" &&
            x.Division == "новий підрозділ" &&
            x.Id != existingResolved.Id);
    }

    [Fact]
    public async Task DismissAsync_MarksGroupAsDismissed_AndSetsResolvedAuditFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        var group = ParticipantCandidateGroup.Create(
            [new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1), new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 2)],
            0.55,
            new PatternMatchReasons { CloseInTime = true });

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        await service.DismissAsync(group.Id, "  analyst  ", ct);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var persistedGroup = await verifyDb.ParticipantCandidateGroups.SingleAsync(ct);
        persistedGroup.Status.Should().Be(CandidateGroupStatus.Dismissed);
        persistedGroup.ResolvedBy.Should().Be("analyst");
        persistedGroup.ResolvedAt.Should().NotBeNull();
        persistedGroup.ResolvedParticipantId.Should().BeNull();
    }

    [Fact]
    public async Task ConfirmAsync_PublishesConfirmedIntegrationEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        var service = new ParticipantCandidateGroupCommandService(factory, publisher);

        var group = ParticipantCandidateGroup.Create(
            [new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1), new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 2)],
            0.79,
            new PatternMatchReasons { SameFrequency = true });

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        await service.ConfirmAsync(group.Id, "ШАПКА", "analyst", "оператор", "1 мсб", ct);

        await publisher.Received(1).PublishAsync(
            Arg.Is<ParticipantCandidateGroupChangedIntegrationEvent>(e =>
                e.GroupId == group.Id && e.ChangeType == ParticipantCandidateGroupChangeType.Confirmed),
            ct);
    }

    [Fact]
    public async Task DismissAsync_PublishesDismissedIntegrationEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var publisher = Substitute.For<IIntegrationEventPublisher>();
        var service = new ParticipantCandidateGroupCommandService(factory, publisher);

        var group = ParticipantCandidateGroup.Create(
            [new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1), new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 2)],
            0.55,
            new PatternMatchReasons { CloseInTime = true });

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        await service.DismissAsync(group.Id, "analyst", ct);

        await publisher.Received(1).PublishAsync(
            Arg.Is<ParticipantCandidateGroupChangedIntegrationEvent>(e =>
                e.GroupId == group.Id && e.ChangeType == ParticipantCandidateGroupChangeType.Dismissed),
            ct);
    }
}
