//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.Application.Reports.Services;
using Interception.Domain.Entities;
using Interception.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Reports;

/// <summary>
/// TDD-тести для DayPictureService під нову модель:
/// підрозділ → епізоди дня → хронологічні записи.
/// Частота є атрибутом запису, а не ключем групування.
/// Вектор сигналу є контекстом запису, а не критерієм візуальної групи.
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

            var todayMessage = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 8, 10, 0, DateTimeKind.Utc),
                "142.4500",
                "336 мсп",
                "північ",
                "ранковий блок");
            todayMessage.AddParticipant("ШАПКА", false, "оператор", 1);

            var yesterdayMessage = CreateMessage(
                action,
                new DateTime(2026, 03, 27, 21, 45, 0, DateTimeKind.Utc),
                "145.1000",
                "186 мсп",
                "південь",
                "вчорашнє повідомлення");
            yesterdayMessage.AddParticipant("ГРОМ", false, "старший", 1);

            db.InterceptionMessages.AddRange(todayMessage, yesterdayMessage);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(day, ct);

        result.TotalMessages.Should().Be(1);
        result.Groups.Should().ContainSingle();

        var entry = result.Groups.Single().Conversations.SelectMany(x => x.Entries).Should().ContainSingle().Subject;
        entry.Note.Should().Be("ранковий блок");
    }

    [Fact]
    public async Task BuildAsync_GroupsMessagesByEffectiveDivision_AndBuildsSingleEpisode_WhenFrequenciesAlternateCloseInTime()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var day = new DateOnly(2026, 03, 28);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var coord = InterceptionAction.Create("координація дій", string.Empty);
            var supply = InterceptionAction.Create("доповідь по забезпеченню", string.Empty);
            db.InterceptionActions.AddRange(coord, supply);

            var m1 = CreateMessage(coord, new DateTime(2026, 03, 28, 4, 25, 0, DateTimeKind.Utc), "157.0250", "656 мсп", "степове-олексіївка", "РАНТИК ПЕРМЯК 09");
            m1.AddParticipant("ВОЛГА", false, "оператор", 1);
            m1.AddParticipant("ИСА", false, "старший", 2);

            var m2 = CreateMessage(supply, new DateTime(2026, 03, 28, 4, 29, 0, DateTimeKind.Utc), "144.4250", "656 мсп", "тернове-темирівка", "ЯГА СОУ летить на БИЧОК");
            m2.AddParticipant("ИСА", false, "старший", 1);
            m2.AddParticipant("ВОЛГА", false, "оператор", 2);

            var m3 = CreateMessage(coord, new DateTime(2026, 03, 28, 4, 34, 0, DateTimeKind.Utc), "157.0250", "656 мсп", "степове-олексіївка", "ЯМАЙКА ЯГНЕНОК");
            m3.AddParticipant("ВОЛГА", false, "оператор", 1);
            m3.AddParticipant("ИСА", false, "старший", 2);

            db.InterceptionMessages.AddRange(m1, m2, m3);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(day, ct);

        result.TotalMessages.Should().Be(3);
        result.Groups.Should().ContainSingle();

        var group = result.Groups.Single();
        group.Division.Should().Be("656 мсп");
        group.Conversations.Should().ContainSingle("близькі за часом повідомлення одного підрозділу мають зібратись в один епізод, навіть якщо частоти чергуються");

        var entries = group.Conversations.Single().Entries;
        entries.Should().HaveCount(3);
        entries.Select(x => x.Frequency).Should().Equal("157.0250", "144.4250", "157.0250");
        entries.Select(x => x.VectorSignal).Should().Equal("степове-олексіївка", "тернове-темирівка", "степове-олексіївка");
    }

    [Fact]
    public async Task BuildAsync_DoesNotUseVectorSignalAsEpisodeGroupingCriterion_WhenTimeIsClose()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var day = new DateOnly(2026, 03, 28);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь по обстановці", string.Empty);
            db.InterceptionActions.Add(action);

            var first = CreateMessage(action, new DateTime(2026, 03, 28, 0, 7, 0, DateTimeKind.Utc), "157.0250", "656 мсп", "степове-олексіївка", "перший вектор");
            first.AddParticipant("ЗВЕЗДА", false, "старший", 1);
            first.AddParticipant("РУС", false, "оператор", 2);

            var second = CreateMessage(action, new DateTime(2026, 03, 28, 0, 12, 0, DateTimeKind.Utc), "157.0250", "656 мсп", "тернове-темирівка", "другий вектор");
            second.AddParticipant("РУС", false, "оператор", 1);
            second.AddParticipant("ЗВЕЗДА", false, "старший", 2);

            db.InterceptionMessages.AddRange(first, second);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(day, ct);

        result.Groups.Should().ContainSingle();
        result.Groups.Single().Conversations.Should().ContainSingle("вектор сигналу є атрибутом запису, а не критерієм розбиття на окремі епізоди");
    }

    [Fact]
    public async Task BuildAsync_SplitsEpisodesWhenTimeGapIsLarge_EvenWithinSameDivision()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var day = new DateOnly(2026, 03, 28);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var first = CreateMessage(action, new DateTime(2026, 03, 28, 10, 5, 0, DateTimeKind.Utc), "142.4500", "336 мсп", "північ", "перша хвиля");
            first.AddParticipant("ГРОМ", false, "старший", 1);

            var second = CreateMessage(action, new DateTime(2026, 03, 28, 10, 12, 0, DateTimeKind.Utc), "145.1000", "336 мсп", "південь", "ще в межах одного епізоду");
            second.AddParticipant("ГРОМ", false, "старший", 1);

            var third = CreateMessage(action, new DateTime(2026, 03, 28, 10, 31, 0, DateTimeKind.Utc), "142.4500", "336 мсп", "північ", "вже новий епізод");
            third.AddParticipant("ГРОМ", false, "старший", 1);

            db.InterceptionMessages.AddRange(first, second, third);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(day, ct);

        var group = result.Groups.Should().ContainSingle().Subject;
        group.Conversations.Should().HaveCount(2);
        group.Conversations[0].Entries.Select(x => x.Note).Should().Equal("перша хвиля", "ще в межах одного епізоду");
        group.Conversations[1].Entries.Select(x => x.Note).Should().Equal("вже новий епізод");
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

            var missingDivision = CreateMessage(action, new DateTime(2026, 03, 28, 9, 5, 0, DateTimeKind.Utc), "142.4500", null, "південь", "без підрозділу");
            missingDivision.AddParticipant("ГРОМ", false, "старший", 1);

            db.InterceptionMessages.AddRange(anchor, missingDivision);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(day, ct);

        result.Groups.Should().ContainSingle();
        var group = result.Groups.Single();
        group.Division.Should().Be("336 мсп");
        group.Conversations.SelectMany(x => x.Entries).Should().HaveCount(2);
    }

    [Fact]
    public async Task BuildAsync_SortsEntriesInsideConversationByObservedDate()
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

            var earlier = CreateMessage(action, new DateTime(2026, 03, 28, 10, 5, 0, DateTimeKind.Utc), "145.1000", "336 мсп", "південь", "раніше");
            earlier.AddParticipant("ГРОМ", false, "старший", 1);

            db.InterceptionMessages.AddRange(later, earlier);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(day, ct);

        var entries = result.Groups.Single().Conversations.Single().Entries;
        entries.Select(x => x.Note).Should().Equal("раніше", "пізніше");
        entries.Select(x => x.Frequency).Should().Equal("145.1000", "142.4500");
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

        var entry = result.Groups.Single().Conversations.Single().Entries.Single();
        entry.Participants.Should().Contain(["ШАПКА", "НВ 1"]);
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
