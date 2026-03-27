//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Interceptions.Services.Registry;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Interceptions.Services.Registry;

/// <summary>
/// TDD-тести для реєстру осіб.
/// </summary>
public sealed class PersonRegistryServiceTests
{
    private static PersonRegistryService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_ReturnsKnownPartialAndConfirmedPersonsSortedByName()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);

            var knownMessage = CreateMessage(action, new DateTime(2026, 03, 21, 10, 00, 00), division: "336 мсп", frequency: "402.0000");
            knownMessage.AddParticipant("АЛЬФА", isUnknown: false, role: "оператор", ordinal: 1);

            var partialMessage = CreateMessage(action, new DateTime(2026, 03, 21, 11, 00, 00), division: null, frequency: "403.0000");
            partialMessage.AddParticipant("БЕТА", isUnknown: false, role: string.Empty, ordinal: 1);

            db.InterceptionActions.Add(action);
            db.InterceptionMessages.AddRange(knownMessage, partialMessage);
            db.ResolvedParticipants.Add(ResolvedParticipant.Create("ГРОМ", "seed", "старший", "БПЛА"));

            await db.SaveChangesAsync(ct);
        }

        var all = await svc.GetAllAsync(ct);

        all.Select(x => x.Name).Should().Equal("АЛЬФА", "БЕТА", "ГРОМ");
        all.Should().ContainSingle(x => x.Name == "АЛЬФА" && x.Role == "оператор" && x.Division == "336 мсп" && !x.IsConfirmed);
        all.Should().ContainSingle(x => x.Name == "БЕТА" && x.Role == null && x.Division == null && !x.IsConfirmed);
        all.Should().ContainSingle(x => x.Name == "ГРОМ" && x.Role == "старший" && x.Division == "БПЛА" && x.IsConfirmed);
    }

    [Fact]
    public async Task GetAllAsync_DoesNotIncludeUnknownParticipants()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            var message = CreateMessage(action, new DateTime(2026, 03, 21, 10, 00, 00), division: "336 мсп", frequency: "402.0000");
            message.AddParticipant("НВ 1", isUnknown: true, role: "невідома", ordinal: 1);
            message.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 2);

            db.InterceptionActions.Add(action);
            db.InterceptionMessages.Add(message);
            await db.SaveChangesAsync(ct);
        }

        var all = await svc.GetAllAsync(ct);

        all.Select(x => x.Name).Should().Equal("ШАПКА");
    }

    [Fact]
    public async Task GetAllAsync_MergesObservedKnownPersonFromSeveralMessages()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);

            var first = CreateMessage(action, new DateTime(2026, 03, 21, 10, 00, 00), division: null, frequency: "402.0000");
            first.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 1);

            var second = CreateMessage(action, new DateTime(2026, 03, 21, 11, 00, 00), division: "336 мсп", frequency: "402.0000");
            second.AddParticipant("ШАПКА", isUnknown: false, role: string.Empty, ordinal: 1);

            db.InterceptionActions.Add(action);
            db.InterceptionMessages.AddRange(first, second);
            await db.SaveChangesAsync(ct);
        }

        var all = await svc.GetAllAsync(ct);

        all.Should().ContainSingle(x => x.Name == "ШАПКА");
        all.Single(x => x.Name == "ШАПКА").Role.Should().Be("оператор");
        all.Single(x => x.Name == "ШАПКА").Division.Should().Be("336 мсп");
        all.Single(x => x.Name == "ШАПКА").IsConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_WhenConfirmedExistsForSameName_ReturnsSingleConfirmedRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            var message = CreateMessage(action, new DateTime(2026, 03, 21, 10, 00, 00), division: "336 мсп", frequency: "402.0000");
            message.AddParticipant("ГРОМ", isUnknown: false, role: "оператор", ordinal: 1);

            db.InterceptionActions.Add(action);
            db.InterceptionMessages.Add(message);
            db.ResolvedParticipants.Add(ResolvedParticipant.Create("ГРОМ", "seed", "старший", "БПЛА"));
            await db.SaveChangesAsync(ct);
        }

        var all = await svc.GetAllAsync(ct);

        all.Should().ContainSingle(x => x.Name == "ГРОМ");
        all.Single(x => x.Name == "ГРОМ").IsConfirmed.Should().BeTrue();
        all.Single(x => x.Name == "ГРОМ").Role.Should().Be("старший");
        all.Single(x => x.Name == "ГРОМ").Division.Should().Be("БПЛА");
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ChangesConfirmedPersonNameRoleAndDivision()
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
    public async Task UpdateAsync_ForObservedKnownPerson_CreatesCanonicalPerson()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        Guid observedId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            var message = CreateMessage(action, new DateTime(2026, 03, 22, 01, 32, 00), division: null, frequency: "402.0000");
            message.AddParticipant("МАНДЖЕСТИК", isUnknown: false, role: null, ordinal: 1);

            observedId = message.Participants.Single().Id;

            db.InterceptionActions.Add(action);
            db.InterceptionMessages.Add(message);
            await db.SaveChangesAsync(ct);
        }

        var dto = await svc.UpdateAsync(
            observedId,
            new PersonRegistryUpdateDto
            {
                Name = "  МАДЖЕСТИК  ",
                Role = "  оператор бпла  ",
                Division = "  ВЖ 1 мсб 656 мсп  "
            },
            ct);

        dto.Name.Should().Be("МАДЖЕСТИК");
        dto.Role.Should().Be("оператор бпла");
        dto.Division.Should().Be("ВЖ 1 мсб 656 мсп");
        dto.IsConfirmed.Should().BeTrue();

        await using (var verifyDb = await factory.CreateDbContextAsync(ct))
        {
            verifyDb.ResolvedParticipants.Should().ContainSingle();

            var resolved = await verifyDb.ResolvedParticipants.SingleAsync(ct);
            resolved.Name.Should().Be("МАДЖЕСТИК");
            resolved.Role.Should().Be("оператор бпла");
            resolved.Division.Should().Be("ВЖ 1 мсб 656 мсп");
        }

        var registry = await svc.GetAllAsync(ct);
        registry.Should().ContainSingle(x => x.Name == "МАДЖЕСТИК" && x.IsConfirmed);
        registry.Should().NotContain(x => x.Name == "МАНДЖЕСТИК");
    }

    [Fact]
    public async Task UpdateAsync_NameConflictWithOtherConfirmedPerson_Throws()
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
            new PersonRegistryUpdateDto { Name = "ГРОМ" },
            ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*вже існує*");
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenPersonNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var act = async () => await svc.UpdateAsync(
            Guid.NewGuid(),
            new PersonRegistryUpdateDto { Name = "ГРОМ" },
            ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*не знайдено*");
    }

    private static InterceptionMessage CreateMessage(
        InterceptionAction action,
        DateTime observedDate,
        string? division,
        string? frequency)
        => InterceptionMessage.Create(
            observedDate,
            frequency,
            division,
            vectorSignal: null,
            action,
            note: null,
            createdBy: "test",
            pointSignal: null);
}
