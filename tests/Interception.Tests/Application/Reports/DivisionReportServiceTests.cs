//----------------------------------------------------------------------------- 
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//----------------------------------------------------------------------------- 

using FluentAssertions;
using Interception.UI.Application.Reports.Services;
using Interception.UI.Domain.Entities;
using Interception.UI.Domain.Records;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Reports;

public sealed class DivisionReportServiceTests
{
    private static DivisionReportService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task BuildAsync_NoMessages_ReturnsEmptyReport()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        var report = await service.BuildAsync(ct: ct);

        report.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_GroupsByDivision()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 26, 9, 0, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            m1.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 1);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 26, 9, 5, 0, DateTimeKind.Utc), division: "186 мсп", frequency: "145.1000");
            m2.AddParticipant("ГРОМ", isUnknown: false, role: "старший", ordinal: 1);

            db.InterceptionMessages.AddRange(m1, m2);
            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        report.Groups.Select(x => x.Division).Should().BeEquivalentTo(["336 мсп", "186 мсп"]);
    }

    [Fact]
    public async Task BuildAsync_UsesEffectiveDivisionFromFrequency_WhenDivisionIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var knownDivision = CreateMessage(
                action,
                new DateTime(2026, 03, 26, 9, 10, 0, DateTimeKind.Utc),
                division: "336 мсп",
                frequency: "142.4500");
            knownDivision.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 1);

            var emptyDivision = CreateMessage(
                action,
                new DateTime(2026, 03, 26, 9, 20, 0, DateTimeKind.Utc),
                division: null,
                frequency: "142.4500");
            emptyDivision.AddParticipant("ГРОМ", isUnknown: false, role: "старший", ordinal: 1);

            db.InterceptionMessages.AddRange(knownDivision, emptyDivision);
            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        report.Groups.Should().ContainSingle();
        var group = report.Groups.Single();

        group.Division.Should().Be("336 мсп");
        group.Frequencies.Should().BeEquivalentTo(["142.4500"]);
        group.People.Select(x => x.Name).Should().BeEquivalentTo(["ШАПКА", "ГРОМ"]);
    }

    [Fact]
    public async Task BuildAsync_AggregatesDistinctFrequenciesInsideDivision()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 26, 10, 0, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            m1.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 1);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 26, 10, 5, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "145.1000");
            m2.AddParticipant("ГРОМ", isUnknown: false, role: "старший", ordinal: 1);

            var m3 = CreateMessage(action, new DateTime(2026, 03, 26, 10, 10, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            m3.AddParticipant("БОНИК", isUnknown: false, role: "оператор", ordinal: 1);

            db.InterceptionMessages.AddRange(m1, m2, m3);
            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        var group = report.Groups.Single(x => x.Division == "336 мсп");
        group.Frequencies.Should().BeEquivalentTo(["142.4500", "145.1000"]);
    }

    [Fact]
    public async Task BuildAsync_DoesNotIncludeUnknownParticipants()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m = CreateMessage(action, new DateTime(2026, 03, 26, 11, 0, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            m.AddParticipant("НВ 1", isUnknown: true, role: "невідома", ordinal: 2);
            m.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 1);

            db.InterceptionMessages.Add(m);
            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        var people = report.Groups.Single().People;
        people.Should().ContainSingle();
        people.Single().Name.Should().Be("ШАПКА");
    }

    [Fact]
    public async Task BuildAsync_IncludesConfirmedPersonInReport()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 26, 11, 0, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            var p1 = m1.AddParticipant("НВ 1", isUnknown: true, role: "невідома", ordinal: 1);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 26, 11, 10, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            var p2 = m2.AddParticipant("НВ 2", isUnknown: true, role: "невідома", ordinal: 1);

            var group = CreateUnknownGroup([p1, p2], [m1, m2], suggestedDivision: "336 мсп");
            var resolved = ResolvedParticipant.Create(
                name: "МАДЖЕСТИК",
                confirmedBy: "analyst",
                role: "оператор бпла",
                division: "336 мсп");

            group.UpdateSuggestedRole("оператор бпла");
            group.UpdateSuggestedDivision("336 мсп");
            group.Confirm("МАДЖЕСТИК", "analyst", resolved.Id);

            db.InterceptionMessages.AddRange(m1, m2);
            db.ResolvedParticipants.Add(resolved);
            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        var person = report.Groups
            .Single(x => x.Division == "336 мсп")
            .People
            .Single(x => x.Name == "МАДЖЕСТИК");

        person.Role.Should().Be("оператор бпла");
        person.LastSeenAt.Should().Be(new DateTime(2026, 03, 26, 11, 10, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task BuildAsync_PrefersConfirmedPersonDivisionOverDominantObservedDivision()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 26, 11, 20, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            var p1 = m1.AddParticipant("НВ 1", isUnknown: true, role: "невідома", ordinal: 1);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 26, 11, 30, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            var p2 = m2.AddParticipant("НВ 2", isUnknown: true, role: "невідома", ordinal: 1);

            var m3 = CreateMessage(action, new DateTime(2026, 03, 26, 11, 40, 0, DateTimeKind.Utc), division: "186 мсп", frequency: "145.1000");
            var p3 = m3.AddParticipant("НВ 3", isUnknown: true, role: "невідома", ordinal: 1);

            var seedGroupMessage = CreateMessage(action, new DateTime(2026, 03, 26, 11, 50, 0, DateTimeKind.Utc), division: "186 мсп", frequency: "145.1000");
            seedGroupMessage.AddParticipant("ГРОМ", isUnknown: false, role: "старший", ordinal: 1);

            var group = CreateUnknownGroup([p1, p2, p3], [m1, m2, m3], suggestedDivision: "336 мсп");
            var resolved = ResolvedParticipant.Create(
                name: "МАДЖЕСТИК",
                confirmedBy: "analyst",
                role: "оператор бпла",
                division: "186 мсп");

            group.UpdateSuggestedRole("оператор бпла");
            group.UpdateSuggestedDivision("336 мсп");
            group.Confirm("МАДЖЕСТИК", "analyst", resolved.Id);

            db.InterceptionMessages.AddRange(m1, m2, m3, seedGroupMessage);
            db.ResolvedParticipants.Add(resolved);
            db.ParticipantCandidateGroups.Add(group);
            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        report.Groups.Single(x => x.Division == "186 мсп")
            .People.Should().ContainSingle(x => x.Name == "МАДЖЕСТИК");

        report.Groups.Single(x => x.Division == "336 мсп")
            .People.Should().NotContain(x => x.Name == "МАДЖЕСТИК");
    }

    [Fact]
    public async Task BuildAsync_ReturnsUnknownMentionsCountPerDivision()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var divisionMessage = CreateMessage(action, new DateTime(2026, 03, 26, 11, 10, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            divisionMessage.AddParticipant("НВ 1", isUnknown: true, role: "невідома", ordinal: 1);
            divisionMessage.AddParticipant("НВ 2", isUnknown: true, role: "невідома", ordinal: 2);
            divisionMessage.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 3);

            var otherDivisionMessage = CreateMessage(action, new DateTime(2026, 03, 26, 11, 20, 0, DateTimeKind.Utc), division: "186 мсп", frequency: "145.1000");
            otherDivisionMessage.AddParticipant("НВ 3", isUnknown: true, role: "невідома", ordinal: 1);

            db.InterceptionMessages.AddRange(divisionMessage, otherDivisionMessage);
            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        report.Groups.Single(x => x.Division == "336 мсп").UnknownMentionsCount.Should().Be(2);
        report.Groups.Single(x => x.Division == "186 мсп").UnknownMentionsCount.Should().Be(1);
    }

    [Fact]
    public async Task BuildAsync_UsesEffectiveDivisionForUnknownMentionsAndUnknownGroups()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var knownDivision = CreateMessage(action, new DateTime(2026, 03, 26, 11, 30, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            knownDivision.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 1);

            var unknown1 = CreateMessage(action, new DateTime(2026, 03, 26, 11, 40, 0, DateTimeKind.Utc), division: null, frequency: "142.4500");
            var p1 = unknown1.AddParticipant("НВ 1", isUnknown: true, role: "невідома", ordinal: 1);

            var unknown2 = CreateMessage(action, new DateTime(2026, 03, 26, 11, 50, 0, DateTimeKind.Utc), division: "НВ підрозділ", frequency: "142.4500");
            var p2 = unknown2.AddParticipant("НВ 2", isUnknown: true, role: "невідома", ordinal: 1);

            var candidateGroup = CreateUnknownGroup([p1, p2], [unknown1, unknown2], suggestedDivision: "НВ підрозділ");

            db.InterceptionMessages.AddRange(knownDivision, unknown1, unknown2);
            db.ParticipantCandidateGroups.Add(candidateGroup);
            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        var group = report.Groups.Single(x => x.Division == "336 мсп");
        group.UnknownMentionsCount.Should().Be(2);
        group.UnknownGroupsCount.Should().Be(1);
    }

    [Fact]
    public async Task BuildAsync_ReturnsUnknownGroupsCountPerDivision()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 26, 11, 30, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            var p1 = m1.AddParticipant("НВ 1", isUnknown: true, role: "невідома", ordinal: 1);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 26, 11, 40, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            var p2 = m2.AddParticipant("НВ 2", isUnknown: true, role: "невідома", ordinal: 1);

            var m3 = CreateMessage(action, new DateTime(2026, 03, 26, 11, 50, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "145.1000");
            var p3 = m3.AddParticipant("НВ 3", isUnknown: true, role: "невідома", ordinal: 1);

            var m4 = CreateMessage(action, new DateTime(2026, 03, 26, 12, 0, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "145.1000");
            var p4 = m4.AddParticipant("НВ 4", isUnknown: true, role: "невідома", ordinal: 1);

            var otherDivisionMessage1 = CreateMessage(action, new DateTime(2026, 03, 26, 12, 10, 0, DateTimeKind.Utc), division: "186 мсп", frequency: "145.1000");
            var otherDivisionParticipant1 = otherDivisionMessage1.AddParticipant("НВ 5", isUnknown: true, role: "невідома", ordinal: 1);

            var otherDivisionMessage2 = CreateMessage(action, new DateTime(2026, 03, 26, 12, 20, 0, DateTimeKind.Utc), division: "186 мсп", frequency: "145.1000");
            var otherDivisionParticipant2 = otherDivisionMessage2.AddParticipant("НВ 6", isUnknown: true, role: "невідома", ordinal: 1);

            db.InterceptionMessages.AddRange(m1, m2, m3, m4, otherDivisionMessage1, otherDivisionMessage2);

            db.ParticipantCandidateGroups.AddRange(
                CreateUnknownGroup([p1, p2], [m1, m2], suggestedDivision: "336 мсп"),
                CreateUnknownGroup([p3, p4], [m3, m4], suggestedDivision: "336 мсп"),
                CreateUnknownGroup([otherDivisionParticipant1, otherDivisionParticipant2], [otherDivisionMessage1, otherDivisionMessage2], suggestedDivision: "186 мсп"));

            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        report.Groups.Single(x => x.Division == "336 мсп").UnknownGroupsCount.Should().Be(2);
        report.Groups.Single(x => x.Division == "186 мсп").UnknownGroupsCount.Should().Be(1);
    }

    [Fact]
    public async Task BuildAsync_IgnoresNonOpenUnknownGroupsInUnknownGroupsCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 26, 12, 20, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            var p1 = m1.AddParticipant("НВ 1", isUnknown: true, role: "невідома", ordinal: 1);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 26, 12, 30, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            var p2 = m2.AddParticipant("НВ 2", isUnknown: true, role: "невідома", ordinal: 1);

            var m3 = CreateMessage(action, new DateTime(2026, 03, 26, 12, 40, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "145.1000");
            var p3 = m3.AddParticipant("НВ 3", isUnknown: true, role: "невідома", ordinal: 1);

            var m4 = CreateMessage(action, new DateTime(2026, 03, 26, 12, 50, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "145.1000");
            var p4 = m4.AddParticipant("НВ 4", isUnknown: true, role: "невідома", ordinal: 1);

            var openGroup = CreateUnknownGroup([p1, p2], [m1, m2], suggestedDivision: "336 мсп");
            var dismissedGroup = CreateUnknownGroup([p3, p4], [m3, m4], suggestedDivision: "336 мсп");
            dismissedGroup.Dismiss("analyst");

            db.InterceptionMessages.AddRange(m1, m2, m3, m4);
            db.ParticipantCandidateGroups.AddRange(openGroup, dismissedGroup);
            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        report.Groups.Single(x => x.Division == "336 мсп").UnknownGroupsCount.Should().Be(1);
    }

    [Fact]
    public async Task BuildAsync_AggregatesSameKnownPersonIntoSingleRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 26, 13, 0, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            m1.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 1);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 26, 13, 10, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "145.1000");
            m2.AddParticipant("ШАПКА", isUnknown: false, role: "старший оператор", ordinal: 1);

            db.InterceptionMessages.AddRange(m1, m2);
            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        var people = report.Groups.Single(x => x.Division == "336 мсп").People;
        people.Should().ContainSingle();
        people.Single().Name.Should().Be("ШАПКА");
    }

    [Fact]
    public async Task BuildAsync_UsesLastNonEmptyRoleAndLastSeenDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var oldMessage = CreateMessage(action, new DateTime(2026, 03, 26, 14, 0, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            oldMessage.AddParticipant("ГРОМ", isUnknown: false, role: "оператор", ordinal: 1);

            var newMessage = CreateMessage(action, new DateTime(2026, 03, 26, 14, 10, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "145.1000");
            newMessage.AddParticipant("ГРОМ", isUnknown: false, role: "старший", ordinal: 1);

            db.InterceptionMessages.AddRange(oldMessage, newMessage);
            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        var person = report.Groups.Single(x => x.Division == "336 мсп")
            .People.Single(x => x.Name == "ГРОМ");

        person.Role.Should().Be("старший");
        person.LastSeenAt.Should().Be(new DateTime(2026, 03, 26, 14, 10, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task BuildAsync_DoesNotAutoMergeObservedKnownPersonAcrossDifferentDivisions_WithoutExplicitUnion()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 26, 15, 0, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            m1.AddParticipant("ЯКУТ", isUnknown: false, role: "оператор", ordinal: 1);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 26, 15, 5, 0, DateTimeKind.Utc), division: null, frequency: "142.4500");
            m2.AddParticipant("ЯКУТ", isUnknown: false, role: null, ordinal: 1);

            var m3 = CreateMessage(action, new DateTime(2026, 03, 26, 15, 10, 0, DateTimeKind.Utc), division: "186 мсп", frequency: "145.1000");
            m3.AddParticipant("ЯКУТ", isUnknown: false, role: "старший", ordinal: 1);

            db.InterceptionMessages.AddRange(m1, m2, m3);
            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        report.Groups.Single(x => x.Division == "336 мсп")
            .People.Should().ContainSingle(x => x.Name == "ЯКУТ");

        report.Groups.Single(x => x.Division == "186 мсп")
            .People.Should().ContainSingle(x => x.Name == "ЯКУТ");
    }

    [Fact]
    public async Task BuildAsync_UsesCanonicalDisplayName_ForConfirmedAndObservedRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message = CreateMessage(action, new DateTime(2026, 03, 26, 16, 0, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            var unknownParticipant = message.AddParticipant("НВ 1", isUnknown: true, role: "невідома", ordinal: 1);
            message.AddParticipant("ШАПКА-2", isUnknown: false, role: "координатор", ordinal: 2);

            var followUpMessage = CreateMessage(action, new DateTime(2026, 03, 26, 16, 5, 0, DateTimeKind.Utc), division: "336 мсп", frequency: "142.4500");
            var followUpUnknownParticipant = followUpMessage.AddParticipant("НВ 2", isUnknown: true, role: "невідома", ordinal: 1);
            db.InterceptionMessages.AddRange(message, followUpMessage);

            var resolvedA = ResolvedParticipant.Create("ШАПКА-1", "analyst", role: "координатор", division: "336 мсп");
            var resolvedB = ResolvedParticipant.Create("ШАПКА-2", "analyst", role: "координатор", division: "336 мсп");
            db.ResolvedParticipants.AddRange(resolvedA, resolvedB);

            var canonical = CanonicalPerson.Create("ШАПКА");
            canonical.AddMember(resolvedA.Id);
            canonical.AddMember(resolvedB.Id);
            db.CanonicalPersons.Add(canonical);

            var candidateGroup = CreateUnknownGroup(
                [unknownParticipant, followUpUnknownParticipant],
                [message, followUpMessage],
                suggestedDivision: "336 мсп");
            candidateGroup.Confirm("ШАПКА-1", "analyst", resolvedA.Id);
            db.ParticipantCandidateGroups.Add(candidateGroup);

            await db.SaveChangesAsync(ct);
        }

        var report = await service.BuildAsync(ct: ct);

        var people = report.Groups.Single(x => x.Division == "336 мсп").People;
        people.Should().ContainSingle(x => x.Name == "ШАПКА");
        people.Should().NotContain(x => x.Name == "ШАПКА-1");
        people.Should().NotContain(x => x.Name == "ШАПКА-2");
    }

    private static InterceptionMessage CreateMessage(
        InterceptionAction action,
        DateTime observedDate,
        string? division,
        string? frequency,
        string? pointSignal = null,
        string? note = null,
        string? vectorSignal = null)
        => InterceptionMessage.Create(
            observedDate,
            frequency,
            division,
            vectorSignal,
            action,
            note,
            createdBy: "test",
            pointSignal: pointSignal);

    private static ParticipantCandidateGroup CreateUnknownGroup(
        IReadOnlyList<InterceptionMessageParticipant> participants,
        IReadOnlyList<InterceptionMessage> messages,
        string suggestedDivision)
    {
        if (participants.Count != messages.Count)
            throw new ArgumentException("Participants and messages must have the same count.");

        var refs = participants
            .Zip(messages, static (participant, message) => new ParticipantRef(message.Id, participant.Id, participant.Ordinal))
            .ToList();

        return ParticipantCandidateGroup.Create(
            refs,
            confidenceScore: 0.85,
            reasons: new PatternMatchReasons { SameDivision = true },
            suggestedDivision: suggestedDivision);
    }
}
