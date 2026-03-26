//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Interceptions.Services.Candidates;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Domain.Records;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Interceptions.Services.Candidates;

public sealed class ResolvedParticipantServiceTests
{
    private static ResolvedParticipantService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    // -------------------------------------------------------------------------
    // ConfirmGroupAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConfirmGroupAsync_OpenGroup_CreatesResolvedParticipantAndConfirmsGroup()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var participantId1 = Guid.NewGuid();
        var participantId2 = Guid.NewGuid();
        var group = ParticipantCandidateGroup.Create(
                refs:
                [
                    new ParticipantRef(Guid.NewGuid(), participantId1, 1),
                    new ParticipantRef(Guid.NewGuid(), participantId2, 1)
                ],
                confidenceScore: 0.82,
                reasons: new PatternMatchReasons { SameFrequency = true, SameVector = true },
                suggestedName: "ГРОМ ?");

        var groupId = group.Id;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        var form = new ConfirmCandidateGroupDto
        {
            Name = "  ГРОМ  ",
            Role = "  оператор  ",
            Division = "  БПЛА  "
        };

        var dto = await svc.ConfirmGroupAsync(groupId, form, "analyst", ct);

        dto.Name.Should().Be("ГРОМ");
        dto.Role.Should().Be("оператор");
        dto.Division.Should().Be("БПЛА");
        dto.ConfirmedBy.Should().Be("analyst");
        dto.Id.Should().NotBeEmpty();

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var result = await verifyDb.ParticipantCandidateGroups.SingleAsync(g => g.Id == groupId, ct);
        var resolved = await verifyDb.ResolvedParticipants.SingleAsync(ct);

        resolved.Name.Should().Be("ГРОМ");
        resolved.Role.Should().Be("оператор");
        resolved.Division.Should().Be("БПЛА");
        resolved.ConfirmedBy.Should().Be("analyst");

        result.Status.Should().Be(CandidateGroupStatus.Confirmed);
        result.SuggestedName.Should().Be("ГРОМ");
        result.SuggestedRole.Should().Be("оператор");
        result.SuggestedDivision.Should().Be("БПЛА");
        result.ResolvedParticipantId.Should().Be(resolved.Id);
        result.ResolvedBy.Should().Be("analyst");
        result.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ConfirmGroupAsync_DismissesOtherOpenGroupsThatContainSameParticipants()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var sharedParticipantId = Guid.NewGuid();
        var main = ParticipantCandidateGroup.Create(
                refs:
                [
                    new ParticipantRef(Guid.NewGuid(), sharedParticipantId, 1),
                    new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1)
                ],
                confidenceScore: 0.80,
                reasons: new PatternMatchReasons { SameFrequency = true });

        var duplicate = ParticipantCandidateGroup.Create(
                refs:
                [
                    new ParticipantRef(Guid.NewGuid(), sharedParticipantId, 1),
                    new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1)
                ],
                confidenceScore: 0.61,
                reasons: new PatternMatchReasons { SameVector = true });

        var mainGroupId = main.Id;
        var duplicateGroupId = duplicate.Id;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ParticipantCandidateGroups.AddRange(main, duplicate);
            await db.SaveChangesAsync(ct);
        }

        await svc.ConfirmGroupAsync(
            mainGroupId,
            new ConfirmCandidateGroupDto { Name = "ШАПКА" },
            "analyst",
            ct);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var groups = await verifyDb.ParticipantCandidateGroups
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

        groups.Single(g => g.Id == mainGroupId).Status.Should().Be(CandidateGroupStatus.Confirmed);
        groups.Single(g => g.Id == duplicateGroupId).Status.Should().Be(CandidateGroupStatus.Dismissed);
        groups.Single(g => g.Id == duplicateGroupId).ResolvedBy.Should().Be("analyst");
    }

    [Fact]
    public async Task ConfirmGroupAsync_GroupWithTwoUnknownsFromSameObservation_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var sameMessageId = Guid.NewGuid();
        var group = ParticipantCandidateGroup.Create(
                refs:
                [
                    new ParticipantRef(sameMessageId, Guid.NewGuid(), 1),
                    new ParticipantRef(sameMessageId, Guid.NewGuid(), 2)
                ],
                confidenceScore: 0.76,
                reasons: new PatternMatchReasons { SameFrequency = true });

        var groupId = group.Id;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        var act = async () => await svc.ConfirmGroupAsync(
            groupId,
            new ConfirmCandidateGroupDto { Name = "БОНИК" },
            "analyst",
            ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*кілька НВ з одного спостереження*");
    }

    [Fact]
    public async Task ConfirmGroupAsync_NameAlreadyExists_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var group = ParticipantCandidateGroup.Create(
                refs:
                [
                    new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1),
                    new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1)
                ],
                confidenceScore: 0.70,
                reasons: new PatternMatchReasons { SameFrequency = true });

        var groupId = group.Id;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ResolvedParticipants.Add(ResolvedParticipant.Create("ГРОМ", "seed"));
            db.ParticipantCandidateGroups.Add(group);

            await db.SaveChangesAsync(ct);
        }

        var act = async () => await svc.ConfirmGroupAsync(
            groupId,
            new ConfirmCandidateGroupDto { Name = "ГРОМ" },
            "analyst",
            ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*вже існує*");
    }

    [Fact]
    public async Task ConfirmGroupAsync_NotOpenGroup_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var group = ParticipantCandidateGroup.Create(
                refs:
                [
                    new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1),
                    new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1)
                ],
                confidenceScore: 0.80,
                reasons: new PatternMatchReasons { SameFrequency = true });
        var groupId = group.Id;
        group.Dismiss("seed");

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        var act = async () => await svc.ConfirmGroupAsync(
            groupId,
            new ConfirmCandidateGroupDto { Name = "ШТОРМ" },
            "analyst",
            ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*статусом*");
    }

    // -------------------------------------------------------------------------
    // DismissGroupAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DismissGroupAsync_OpenGroup_SetsDismissedStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var group = ParticipantCandidateGroup.Create(
                refs:
                [
                    new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1),
                    new ParticipantRef(Guid.NewGuid(), Guid.NewGuid(), 1)
                ],
                confidenceScore: 0.55,
                reasons: new PatternMatchReasons { SharedLabels = true });

        var groupId = group.Id;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        await svc.DismissGroupAsync(groupId, "analyst", ct);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var result = await verifyDb.ParticipantCandidateGroups.SingleAsync(g => g.Id == groupId, ct);

        result.Status.Should().Be(CandidateGroupStatus.Dismissed);
        result.ResolvedBy.Should().Be("analyst");
        result.ResolvedAt.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_ReturnsResolvedParticipantsSortedByName()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ResolvedParticipants.Add(ResolvedParticipant.Create("ШТОРМ", "seed"));
            db.ResolvedParticipants.Add(ResolvedParticipant.Create("БОНИК", "seed"));
            db.ResolvedParticipants.Add(ResolvedParticipant.Create("ГРОМ", "seed"));
            await db.SaveChangesAsync(ct);
        }

        var all = await svc.GetAllAsync(ct);

        all.Select(x => x.Name).Should().Equal("БОНИК", "ГРОМ", "ШТОРМ");
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ChangesNameRoleAndDivision()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        Guid id;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var resolved = ResolvedParticipant.Create("ГРОМ", "seed", "оператор", "БПЛА");
            id = resolved.Id;
            db.ResolvedParticipants.Add(resolved);
            await db.SaveChangesAsync(ct);
        }

        var dto = await svc.UpdateAsync(
            id,
            new ConfirmCandidateGroupDto
            {
                Name = "  ШТОРМ  ",
                Role = "  старший  ",
                Division = "  РЕР  "
            },
            ct);

        dto.Name.Should().Be("ШТОРМ");
        dto.Role.Should().Be("старший");
        dto.Division.Should().Be("РЕР");

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var result = await verifyDb.ResolvedParticipants.SingleAsync(r => r.Id == id, ct);
        result.Name.Should().Be("ШТОРМ");
        result.Role.Should().Be("старший");
        result.Division.Should().Be("РЕР");
    }

    [Fact]
    public async Task UpdateAsync_NameConflictWithOther_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        Guid id;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var a = ResolvedParticipant.Create("ГРОМ", "seed");
            var b = ResolvedParticipant.Create("БОНИК", "seed");
            id = b.Id;
            db.ResolvedParticipants.AddRange(a, b);
            await db.SaveChangesAsync(ct);
        }

        var act = async () => await svc.UpdateAsync(
            id,
            new ConfirmCandidateGroupDto { Name = "ГРОМ" },
            ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*вже існує*");
    }

}
