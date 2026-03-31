//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Analytics.Services;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Domain.Records;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Analytics;

public sealed class ParticipantCandidateGroupCommandServiceTests
{
    private static ParticipantCandidateGroupCommandService CreateService(IDbContextFactory<Interception.UI.Infrastructure.AppDbContext> factory)
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
    public async Task ConfirmAsync_WhenResolvedParticipantExistsByName_UpdatesItWithoutCreatingDuplicate()
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
}
