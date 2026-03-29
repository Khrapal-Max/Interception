//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Interceptions.Services.Candidates;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Interceptions.Services.Candidates;

/// <summary>
/// TDD-тести для карти зв'язків.
/// Частина кейсів фіксує вже прийняту базову поведінку,
/// а частина навмисно закодовує вимоги стабільної моделі,
/// які поточна frequency-first реалізація ще не виконує.
/// </summary>
public sealed class LinkMapServiceTests
{
    private static LinkMapService CreateService(IDbContextFactory<Interception.UI.Infrastructure.AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task BuildAsync_NoMessages_ReturnsEmptyGroups()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_DoesNotCreateGroupsFromUnknownOnlyParticipants()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 9, 0, 0, DateTimeKind.Utc),
                division: null,
                frequency: "402.0000");

            message.AddParticipant("НВ 1", isUnknown: true, role: "невідомий", ordinal: 1);
            message.AddParticipant("НВ 2", isUnknown: true, role: "невідомий", ordinal: 2);

            db.InterceptionMessages.Add(message);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_CreatesGroupWithoutDivision_WhenCommunicationPatternExists()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 10, 0, 0, DateTimeKind.Utc),
                division: null,
                frequency: "402.0000");
            m1.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m1.AddParticipant("А", false, "оператор", 2);

            var m2 = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 10, 5, 0, DateTimeKind.Utc),
                division: null,
                frequency: "402.0000");
            m2.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m2.AddParticipant("Б", false, "оператор", 2);

            db.InterceptionMessages.AddRange(m1, m2);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Groups.Should().ContainSingle();

        var group = result.Groups.Single();
        group.Division.Should().BeNull();
        group.KeyPersonName.Should().Be("ЦЕНТР");
        group.KeyPersonRole.Should().Be("координатор");
        group.Members.Should().BeEquivalentTo(["ЦЕНТР", "А", "Б"]);
        group.Frequencies.Should().BeEquivalentTo(["402.0000"]);
    }

    [Fact]
    public async Task BuildAsync_InfersDivisionForGroup_WhenKnownDivisionSignalsExist()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var seed = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 11, 0, 0, DateTimeKind.Utc),
                division: "336 мсп",
                frequency: "402.0000");
            seed.AddParticipant("ЦЕНТР", false, "координатор", 1);
            seed.AddParticipant("А", false, "оператор", 2);

            var second = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 11, 5, 0, DateTimeKind.Utc),
                division: null,
                frequency: "402.0000");
            second.AddParticipant("ЦЕНТР", false, "координатор", 1);
            second.AddParticipant("Б", false, "оператор", 2);

            db.InterceptionMessages.AddRange(seed, second);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Groups.Should().ContainSingle();
        result.Groups.Single().Division.Should().Be("336 мсп");
    }

    [Fact]
    public async Task BuildAsync_CreatesBridgeBetweenGroupsWithoutRequiringDivision()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            SeedTwoIndependentGroupsWithBridge(db, action, includeSecondBridgeFrequency: false);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Groups.Should().HaveCount(2);

        var groupA = result.Groups.Single(x => x.KeyPersonName == "А-ЦЕНТР");
        var groupB = result.Groups.Single(x => x.KeyPersonName == "Б-ЦЕНТР");

        groupA.Division.Should().BeNull();
        groupB.Division.Should().BeNull();

        groupA.Bridges.Should().ContainSingle();
        var bridgeFromA = groupA.Bridges.Single();
        bridgeFromA.TargetGroupKey.Should().Be(groupB.GroupKey);
        bridgeFromA.TargetDivision.Should().BeNull();
        bridgeFromA.BridgeFrequency.Should().Be("401.2000");
        bridgeFromA.ContactPersonName.Should().Be("Б-ЦЕНТР");
        bridgeFromA.Weight.Should().Be(2);
    }

    [Fact]
    public async Task BuildAsync_PrefersCenterAsKeyPerson_WhenOnePersonConnectsWholeGroup()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 13, 0, 0, DateTimeKind.Utc),
                division: null,
                frequency: "402.0000");
            m1.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m1.AddParticipant("А", false, "оператор", 2);

            var m2 = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 13, 5, 0, DateTimeKind.Utc),
                division: null,
                frequency: "402.0000");
            m2.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m2.AddParticipant("Б", false, "оператор", 2);

            var delta = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 13, 10, 0, DateTimeKind.Utc),
                division: null,
                frequency: "402.0000");
            delta.AddParticipant("А", false, "оператор", 1);
            delta.AddParticipant("Б", false, "оператор", 2);

            db.InterceptionMessages.AddRange(m1, m2, delta);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Groups.Should().ContainSingle();
        result.Groups.Single().KeyPersonName.Should().Be("ЦЕНТР");
    }

    [Fact]
    public async Task BuildAsync_MergesStableCoreAcrossDifferentFrequencies_IntoSingleGroup()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            SeedStableCoreOnFrequency(db, action, "402.0000", new DateTime(2026, 03, 28, 14, 0, 0, DateTimeKind.Utc));
            SeedStableCoreOnFrequency(db, action, "145.1000", new DateTime(2026, 03, 28, 14, 20, 0, DateTimeKind.Utc));
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Groups.Should().ContainSingle(
            "стале комунікаційне ядро не повинно розщеплюватись лише через зміну carrier-frequency");

        var group = result.Groups.Single();
        group.KeyPersonName.Should().Be("ЦЕНТР");
        group.Members.Should().BeEquivalentTo(["ЦЕНТР", "А", "Б"]);
        group.Frequencies.Should().BeEquivalentTo(["145.1000", "402.0000"]);
    }

    [Fact]
    public async Task BuildAsync_DoesNotAddBridgeOnlyFrequency_ToGroupCarrierFrequencies()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            SeedTwoIndependentGroupsWithBridge(db, action, includeSecondBridgeFrequency: false);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Groups.Should().HaveCount(2);

        var groupA = result.Groups.Single(x => x.KeyPersonName == "А-ЦЕНТР");
        var groupB = result.Groups.Single(x => x.KeyPersonName == "Б-ЦЕНТР");

        groupA.Frequencies.Should().BeEquivalentTo(
            ["402.0000"],
            "carrier-frequency групи має відображати її власний внутрішній контур, а не міст");

        groupB.Frequencies.Should().BeEquivalentTo(
            ["145.1000"],
            "bridge-frequency має жити на мосту, а не забруднювати список carrier-frequency групи");

        groupA.Bridges.Single().BridgeFrequency.Should().Be("401.2000");
        groupB.Bridges.Single().BridgeFrequency.Should().Be("401.2000");
    }

    [Fact]
    public async Task BuildAsync_AggregatesBridgeWeightAcrossAllBridgeFrequencies()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            SeedTwoIndependentGroupsWithBridge(db, action, includeSecondBridgeFrequency: true);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Groups.Should().HaveCount(2);

        var groupA = result.Groups.Single(x => x.KeyPersonName == "А-ЦЕНТР");
        var groupB = result.Groups.Single(x => x.KeyPersonName == "Б-ЦЕНТР");

        var bridgeFromA = groupA.Bridges.Single(x => x.TargetGroupKey == groupB.GroupKey);
        var bridgeFromB = groupB.Bridges.Single(x => x.TargetGroupKey == groupA.GroupKey);

        bridgeFromA.BridgeFrequency.Should().Be("401.2000", "найсильніша bridge-frequency має лишатись основною міткою моста");
        bridgeFromA.Weight.Should().Be(3, "вага моста має накопичувати всі observation мосту, а не лише observation на одній frequency");

        bridgeFromB.BridgeFrequency.Should().Be("401.2000");
        bridgeFromB.Weight.Should().Be(3);
    }

    private static void SeedStableCoreOnFrequency(
        AppDbContext db,
        InterceptionAction action,
        string frequency,
        DateTime baseTimeUtc)
    {
        var m1 = CreateMessage(action, baseTimeUtc, division: null, frequency: frequency);
        m1.AddParticipant("ЦЕНТР", false, "координатор", 1);
        m1.AddParticipant("А", false, "оператор", 2);

        var m2 = CreateMessage(action, baseTimeUtc.AddMinutes(5), division: null, frequency: frequency);
        m2.AddParticipant("ЦЕНТР", false, "координатор", 1);
        m2.AddParticipant("Б", false, "оператор", 2);

        db.InterceptionMessages.AddRange(m1, m2);
    }

    private static void SeedTwoIndependentGroupsWithBridge(
        Interception.UI.Infrastructure.AppDbContext db,
        InterceptionAction action,
        bool includeSecondBridgeFrequency)
    {
        var a1 = CreateMessage(
            action,
            new DateTime(2026, 03, 28, 12, 0, 0, DateTimeKind.Utc),
            division: null,
            frequency: "402.0000");
        a1.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        a1.AddParticipant("А-1", false, "оператор", 2);

        var a2 = CreateMessage(
            action,
            new DateTime(2026, 03, 28, 12, 1, 0, DateTimeKind.Utc),
            division: null,
            frequency: "402.0000");
        a2.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        a2.AddParticipant("А-2", false, "оператор", 2);

        var b1 = CreateMessage(
            action,
            new DateTime(2026, 03, 28, 12, 2, 0, DateTimeKind.Utc),
            division: null,
            frequency: "145.1000");
        b1.AddParticipant("Б-ЦЕНТР", false, "координатор", 1);
        b1.AddParticipant("Б-1", false, "оператор", 2);

        var b2 = CreateMessage(
            action,
            new DateTime(2026, 03, 28, 12, 3, 0, DateTimeKind.Utc),
            division: null,
            frequency: "145.1000");
        b2.AddParticipant("Б-ЦЕНТР", false, "координатор", 1);
        b2.AddParticipant("Б-2", false, "оператор", 2);

        var bridge1 = CreateMessage(
            action,
            new DateTime(2026, 03, 28, 12, 4, 0, DateTimeKind.Utc),
            division: null,
            frequency: "401.2000");
        bridge1.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        bridge1.AddParticipant("Б-ЦЕНТР", false, "координатор", 2);

        var bridge2 = CreateMessage(
            action,
            new DateTime(2026, 03, 28, 12, 5, 0, DateTimeKind.Utc),
            division: null,
            frequency: "401.2000");
        bridge2.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        bridge2.AddParticipant("Б-ЦЕНТР", false, "координатор", 2);

        db.InterceptionMessages.AddRange(a1, a2, b1, b2, bridge1, bridge2);

        if (!includeSecondBridgeFrequency)
            return;

        var bridge3 = CreateMessage(
            action,
            new DateTime(2026, 03, 28, 12, 6, 0, DateTimeKind.Utc),
            division: null,
            frequency: "401.5000");
        bridge3.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        bridge3.AddParticipant("Б-ЦЕНТР", false, "координатор", 2);

        db.InterceptionMessages.Add(bridge3);
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
