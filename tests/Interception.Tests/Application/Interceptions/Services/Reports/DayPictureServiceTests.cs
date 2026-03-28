//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Interceptions.Services.Reports;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Interceptions.Services.Reports;

/// <summary>
/// TDD-тести для DayPictureService.
/// </summary>
public sealed class DayPictureServiceTests
{
    private static DayPictureService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task BuildAsync_NoMessages_ReturnsEmptyPicture()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var day = new DateOnly(2026, 03, 28);

        var result = await service.BuildAsync(day, ct);

        result.Day.Should().Be(day);
        result.TotalMessages.Should().Be(0);
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_ReturnsOnlyRequestedDayMessages()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var day = new DateOnly(2026, 03, 28);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 28, 8, 10, 0, DateTimeKind.Utc), "142.4500", "336 мсп", "північ", "ранковий блок");
            m1.AddParticipant("ШАПКА", false, "оператор", 1);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 27, 21, 45, 0, DateTimeKind.Utc), "145.1000", "186 мсп", "південь", "вчорашнє повідомлення");
            m2.AddParticipant("ГРОМ", false, "старший", 1);

            db.InterceptionMessages.AddRange(m1, m2);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(day, ct);

        result.TotalMessages.Should().Be(1);
        result.Groups.SelectMany(x => x.Entries).Should().ContainSingle();
        result.Groups.SelectMany(x => x.Entries).Single().Note.Should().Be("ранковий блок");
    }

    [Fact]
    public async Task BuildAsync_GroupsMessagesByFrequencyVectorAndEffectiveDivision()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var day = new DateOnly(2026, 03, 28);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 28, 8, 0, 0, DateTimeKind.Utc), "142.4500", "336 мсп", "північ", "пакет 1");
            m1.AddParticipant("ШАПКА", false, "оператор", 1);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 28, 8, 5, 0, DateTimeKind.Utc), "142.4500", "336 мсп", "північ", "пакет 2");
            m2.AddParticipant("ГРОМ", false, "старший", 1);

            var m3 = CreateMessage(action, new DateTime(2026, 03, 28, 8, 10, 0, DateTimeKind.Utc), "145.1000", "336 мсп", "північ", "пакет 3");
            m3.AddParticipant("БОНИК", false, "оператор", 1);

            db.InterceptionMessages.AddRange(m1, m2, m3);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(day, ct);

        result.TotalMessages.Should().Be(3);
        result.Groups.Should().HaveCount(2);

        var grouped = result.Groups.Single(x => x.Frequency == "142.4500" && x.VectorSignal == "північ" && x.Division == "336 мсп");
        grouped.MessageCount.Should().Be(2);
        grouped.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task BuildAsync_UsesFrequencyDivisionWhenObservedDivisionMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var day = new DateOnly(2026, 03, 28);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var anchor = CreateMessage(action, new DateTime(2026, 03, 28, 9, 0, 0, DateTimeKind.Utc), "142.4500", "336 мсп", "північ", "якір");
            anchor.AddParticipant("ШАПКА", false, "оператор", 1);

            var missingDivision = CreateMessage(action, new DateTime(2026, 03, 28, 9, 5, 0, DateTimeKind.Utc), "142.4500", null, "північ", "без підрозділу");
            missingDivision.AddParticipant("ГРОМ", false, "старший", 1);

            db.InterceptionMessages.AddRange(anchor, missingDivision);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(day, ct);

        result.Groups.Should().ContainSingle();
        result.Groups.Single().Division.Should().Be("336 мсп");
        result.Groups.Single().Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task BuildAsync_SortsEntriesInsideGroupByObservedDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var day = new DateOnly(2026, 03, 28);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var later = CreateMessage(action, new DateTime(2026, 03, 28, 10, 15, 0, DateTimeKind.Utc), "142.4500", "336 мсп", "північ", "пізніше");
            later.AddParticipant("ШАПКА", false, "оператор", 1);

            var earlier = CreateMessage(action, new DateTime(2026, 03, 28, 10, 5, 0, DateTimeKind.Utc), "142.4500", "336 мсп", "північ", "раніше");
            earlier.AddParticipant("ГРОМ", false, "старший", 1);

            db.InterceptionMessages.AddRange(later, earlier);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(day, ct);

        var entries = result.Groups.Single().Entries;
        entries.Select(x => x.Note).Should().Equal("раніше", "пізніше");
    }

    [Fact]
    public async Task BuildAsync_IncludesParticipantsInEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var day = new DateOnly(2026, 03, 28);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message = CreateMessage(action, new DateTime(2026, 03, 28, 11, 0, 0, DateTimeKind.Utc), "142.4500", "336 мсп", "північ", "учасники");
            message.AddParticipant("ШАПКА", false, "оператор", 1);
            message.AddParticipant("НВ 1", true, null, 2);

            db.InterceptionMessages.Add(message);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(day, ct);

        var entry = result.Groups.Single().Entries.Single();
        entry.Participants.Should().Contain(new[] { "ШАПКА", "НВ 1" });
    }

    private static InterceptionMessage CreateMessage(
        InterceptionAction action,
        DateTime observedDate,
        string? frequency,
        string? division,
        string? vectorSignal,
        string? note)
        => InterceptionMessage.Create(
            observedDate,
            frequency,
            division,
            vectorSignal,
            action,
            note,
            createdBy: "seed");
}
