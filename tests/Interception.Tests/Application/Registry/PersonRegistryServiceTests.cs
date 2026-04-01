//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.Application.Registry.Dtos;
using Interception.Application.Registry.Services;
using Interception.Domain.Entities;
using Interception.Domain.Records;
using Interception.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Registry;

/// <summary>
/// TDD-тести для реєстру осіб.
/// </summary>
public sealed class PersonRegistryServiceTests
{
    private static PersonRegistryService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task GetAllAsync_ReturnsConfirmedObservedKnownAndPartialPersons()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ResolvedParticipants.Add(ResolvedParticipant.Create("ШТОРМ", "seed", "старший", "РЕР"));

            var action = CreateAction();
            db.InterceptionActions.Add(action);

            var known = CreateMessage(action, division: "РЕР");
            known.AddParticipant("БОНИК", isUnknown: false, role: "оператор");

            var partial = CreateMessage(action, division: null);
            partial.AddParticipant("ГРОМ", isUnknown: false, role: null);

            var unknown = CreateMessage(action, division: "РЕР");
            unknown.AddParticipant("НВ 1", isUnknown: true, role: null);

            db.InterceptionMessages.AddRange(known, partial, unknown);
            await db.SaveChangesAsync(ct);
        }

        var all = await svc.GetAllAsync(ct);

        all.Select(x => x.Name)
            .Should()
            .Contain(["ШТОРМ", "БОНИК", "ГРОМ"])
            .And.NotContain("НВ 1");

        all.Should().ContainSingle(x => x.Name == "ШТОРМ" && x.IsConfirmed);
        all.Should().ContainSingle(x => x.Name == "БОНИК" && !x.IsConfirmed && x.Role == "оператор" && x.Division == "РЕР");
        all.Should().ContainSingle(x => x.Name == "ГРОМ" && !x.IsConfirmed && x.Role == null);
    }

    [Fact]
    public async Task GetAllAsync_DoesNotReturnCandidateGroupsWithoutKnownPerson()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = CreateAction();
            db.InterceptionActions.Add(action);

            var messageA = CreateMessage(action, division: null);
            var p1 = messageA.AddParticipant("НВ 1", isUnknown: true);

            var messageB = CreateMessage(action, division: null);
            var p2 = messageB.AddParticipant("НВ 2", isUnknown: true);

            db.InterceptionMessages.AddRange(messageA, messageB);
            await db.SaveChangesAsync(ct);

            var group = ParticipantCandidateGroup.Create(
                [new ParticipantRef(messageA.Id, p1.Id, p1.Ordinal), new ParticipantRef(messageB.Id, p2.Id, p2.Ordinal)],
                0.87,
                new PatternMatchReasons { SameFrequency = true, SharedPartners = true },
                suggestedName: "ВОВК");

            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        var all = await svc.GetAllAsync(ct);

        all.Select(x => x.Name).Should().NotContain("ВОВК");
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ForConfirmedPerson_UpdatesCanonicalRecord()
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
            new PersonRegistryUpdateDto
            {
                Name = "  ШТОРМ  ",
                Role = "  старший  ",
                Division = "  РЕР  "
            },
            ct);

        dto.Name.Should().Be("ШТОРМ");
        dto.Role.Should().Be("старший");
        dto.Division.Should().Be("РЕР");
        dto.IsConfirmed.Should().BeTrue();

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var result = await verifyDb.ResolvedParticipants.SingleAsync(r => r.Id == id, ct);
        result.Name.Should().Be("ШТОРМ");
        result.Role.Should().Be("старший");
        result.Division.Should().Be("РЕР");
    }

    [Fact]
    public async Task UpdateAsync_ForObservedKnown_CreatesCanonicalPersonAtFirstEdit()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        Guid observedId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = CreateAction();
            db.InterceptionActions.Add(action);

            var message = CreateMessage(action, division: "ВЖ 1 мсб 656 мсп");
            var participant = message.AddParticipant("МАНДЖЕСТИК", isUnknown: false, role: null);
            observedId = participant.Id;

            db.InterceptionMessages.Add(message);
            await db.SaveChangesAsync(ct);
        }

        var dto = await svc.UpdateAsync(
            observedId,
            new PersonRegistryUpdateDto
            {
                Name = "МАДЖЕСТИК",
                Role = "оператор бпла",
                Division = "ВЖ 1 мсб 656 мсп"
            },
            ct);

        dto.IsConfirmed.Should().BeTrue();
        dto.Name.Should().Be("МАДЖЕСТИК");
        dto.Role.Should().Be("оператор бпла");
        dto.Division.Should().Be("ВЖ 1 мсб 656 мсп");

        await using (var verifyDb = await factory.CreateDbContextAsync(ct))
        {
            verifyDb.ResolvedParticipants.Should().ContainSingle(x =>
                x.Name == "МАДЖЕСТИК" &&
                x.Role == "оператор бпла" &&
                x.Division == "ВЖ 1 мсб 656 мсп");

            var participant = await verifyDb.InterceptionMessages
                .SelectMany(x => x.Participants)
                .SingleAsync(x => x.Id == observedId, ct);

            participant.IsUnknown.Should().BeFalse();
            participant.Name.Should().Be("МАДЖЕСТИК");
            participant.Role.Should().Be("оператор бпла");
        }

        var all = await svc.GetAllAsync(ct);
        all.Should().ContainSingle(x => x.Name == "МАДЖЕСТИК");
        all.Should().NotContain(x => x.Name == "МАНДЖЕСТИК");
    }

    [Fact]
    public async Task UpdateAsync_ForPartialPerson_CreatesCanonicalPersonAtFirstEdit()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        Guid partialId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = CreateAction();
            db.InterceptionActions.Add(action);

            var message = CreateMessage(action, division: null);
            var participant = message.AddParticipant("ГРОМ", isUnknown: false, role: null);
            partialId = participant.Id;

            db.InterceptionMessages.Add(message);
            await db.SaveChangesAsync(ct);
        }

        var dto = await svc.UpdateAsync(
            partialId,
            new PersonRegistryUpdateDto
            {
                Name = "ГРОМ",
                Role = "старший",
                Division = "РЕР"
            },
            ct);

        dto.IsConfirmed.Should().BeTrue();
        dto.Name.Should().Be("ГРОМ");
        dto.Role.Should().Be("старший");
        dto.Division.Should().Be("РЕР");

        await using (var verifyDb = await factory.CreateDbContextAsync(ct))
        {
            verifyDb.ResolvedParticipants.Should().ContainSingle(x =>
                x.Name == "ГРОМ" &&
                x.Role == "старший" &&
                x.Division == "РЕР");
        }

        var all = await svc.GetAllAsync(ct);
        all.Should().ContainSingle(x => x.Name == "ГРОМ");
    }

    private static InterceptionAction CreateAction()
        => InterceptionAction.Create("Робота", "seed");

    private static InterceptionMessage CreateMessage(
        InterceptionAction action,
        string? division)
        => InterceptionMessage.Create(
            observedDate: new DateTime(2026, 03, 28, 10, 00, 00, DateTimeKind.Utc),
            frequency: "402.0000",
            division: division,
            vectorSignal: null,
            interceptionAction: action,
            note: null,
            createdBy: "seed",
            pointSignal: null);
}
