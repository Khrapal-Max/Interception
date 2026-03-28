//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Interceptions.Models.PatternRecognition;
using Interception.UI.Application.Interceptions.Services.Candidates;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Interceptions.Services.Candidates;

/// <summary>
/// TDD-тести для карти зв'язків між особами.
/// </summary>
public sealed class LinkMapServiceTests
{
    private static LinkMapService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task BuildAsync_NoMessages_ReturnsEmptyMap()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        var result = await service.BuildAsync(ct: ct);

        result.Nodes.Should().BeEmpty();
        result.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_BuildsNodesAndEdgesOnlyFromKnownParticipants()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message = CreateMessage(action,
                new DateTime(2026, 03, 28, 9, 0, 0, DateTimeKind.Utc),
                division: "336 мсп",
                frequency: "402.0000");
            message.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 1);
            message.AddParticipant("ГРОМ", isUnknown: false, role: "старший", ordinal: 2);
            message.AddParticipant("НВ 1", isUnknown: true, role: "невідомий", ordinal: 3);

            db.InterceptionMessages.Add(message);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: ct);

        result.Nodes.Select(x => x.Name).Should().BeEquivalentTo(["ШАПКА", "ГРОМ"]);
        result.Edges.Should().ContainSingle();

        var edge = result.Edges.Single();
        new[] { edge.FromPersonKey, edge.ToPersonKey }.Should().BeEquivalentTo(["ШАПКА", "ГРОМ"]);
        edge.Weight.Should().Be(1);
    }

    [Fact]
    public async Task BuildAsync_UsesEffectiveDivisionFromFrequency_WhenObservedDivisionMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var seed = CreateMessage(action,
                new DateTime(2026, 03, 28, 10, 0, 0, DateTimeKind.Utc),
                division: "336 мсп",
                frequency: "402.0000");
            seed.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 1);
            seed.AddParticipant("ГРОМ", isUnknown: false, role: "старший", ordinal: 2);

            var messageWithoutDivision = CreateMessage(action,
                new DateTime(2026, 03, 28, 10, 5, 0, DateTimeKind.Utc),
                division: null,
                frequency: "402.0000");
            messageWithoutDivision.AddParticipant("ЦЕНТР", isUnknown: false, role: "координатор", ordinal: 1);
            messageWithoutDivision.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 2);

            db.InterceptionMessages.AddRange(seed, messageWithoutDivision);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: ct);

        var center = result.Nodes.Single(x => x.Name == "ЦЕНТР");
        center.Divisions.Should().ContainSingle("336 мсп");
    }

    [Fact]
    public async Task BuildAsync_MarksLocalCenterCandidate_WhenPersonContactsManyNeighbors()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 28, 11, 0, 0, DateTimeKind.Utc), "336 мсп", "402.0000");
            m1.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m1.AddParticipant("А", false, "оператор", 2);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 28, 11, 5, 0, DateTimeKind.Utc), "336 мсп", "402.0000");
            m2.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m2.AddParticipant("Б", false, "оператор", 2);

            var m3 = CreateMessage(action, new DateTime(2026, 03, 28, 11, 10, 0, DateTimeKind.Utc), "336 мсп", "402.0000");
            m3.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m3.AddParticipant("В", false, "оператор", 2);

            db.InterceptionMessages.AddRange(m1, m2, m3);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: ct);

        var center = result.Nodes.Single(x => x.Name == "ЦЕНТР");
        center.ConnectionCount.Should().Be(3);
        center.IsLocalCenterCandidate.Should().BeTrue();

        result.Nodes.Where(x => x.Name != "ЦЕНТР")
            .Should().OnlyContain(x => x.IsLocalCenterCandidate == false);
    }

    [Fact]
    public async Task BuildAsync_MarksBridgeAndMultiDivisionCandidate_WhenPersonConnectsTwoDivisions()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var left = CreateMessage(action, new DateTime(2026, 03, 28, 12, 0, 0, DateTimeKind.Utc), "336 мсп", "402.0000");
            left.AddParticipant("МІСТ", false, "координатор", 1);
            left.AddParticipant("ШАПКА", false, "оператор", 2);

            var right = CreateMessage(action, new DateTime(2026, 03, 28, 12, 5, 0, DateTimeKind.Utc), "186 мсп", "145.1000");
            right.AddParticipant("МІСТ", false, "координатор", 1);
            right.AddParticipant("ГРОМ", false, "старший", 2);

            db.InterceptionMessages.AddRange(left, right);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: ct);

        var bridge = result.Nodes.Single(x => x.Name == "МІСТ");
        bridge.IsBridgeCandidate.Should().BeTrue();
        bridge.IsMultiDivisionCandidate.Should().BeTrue();
        bridge.Divisions.Should().BeEquivalentTo(["336 мсп", "186 мсп"]);
    }

    [Fact]
    public async Task BuildAsync_AggregatesLabelsIntoEdgeContext()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message = CreateMessage(action,
                new DateTime(2026, 03, 28, 13, 0, 0, DateTimeKind.Utc),
                division: "336 мсп",
                frequency: "402.0000");
            message.AddParticipant("ШАПКА", isUnknown: false, role: "оператор", ordinal: 1);
            message.AddParticipant("ГРОМ", isUnknown: false, role: "старший", ordinal: 2);
            message.AddLabel("БПЛА");
            message.AddLabel("дорозвідка");

            db.InterceptionMessages.Add(message);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: ct);

        var edge = result.Edges.Single();
        edge.Labels.Should().BeEquivalentTo(["БПЛА", "дорозвідка"]);
    }

    [Fact]
    public async Task BuildAsync_AggregatesEdgeWeightAcrossMessages()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 28, 14, 0, 0, DateTimeKind.Utc), "336 мсп", "402.0000");
            m1.AddParticipant("ШАПКА", false, "оператор", 1);
            m1.AddParticipant("ГРОМ", false, "старший", 2);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 28, 14, 5, 0, DateTimeKind.Utc), "336 мсп", "402.0000");
            m2.AddParticipant("ШАПКА", false, "оператор", 1);
            m2.AddParticipant("ГРОМ", false, "старший", 2);

            db.InterceptionMessages.AddRange(m1, m2);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: ct);

        var edge = result.Edges.Single();
        edge.Weight.Should().Be(2);
    }

    private static InterceptionMessage CreateMessage(
        InterceptionAction action,
        DateTime observedDate,
        string? division,
        string frequency,
        string? vectorSignal = "р-н Шевченко")
        => InterceptionMessage.Create(
            observedDate,
            frequency,
            division,
            vectorSignal,
            action,
            note: null,
            createdBy: "seed");
}
