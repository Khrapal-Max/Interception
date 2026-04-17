//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Analytics.Services;
using Interception.UI.Domain.Entities;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Analytics;

/// <summary>
/// TDD-тести для карти зв'язків.
/// Частина кейсів фіксує вже прийняту базову поведінку,
/// а частина навмисно закодовує вимоги стабільної моделі,
/// включно з аналітичним шаром по діях груп і мостів.
/// </summary>
public sealed class LinkMapServiceTests
{
    private static LinkMapService CreateService(IDbContextFactory<AppDbContext> factory)
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
    public async Task BuildAsync_AggregatesAllKnownParticipantsInsideSingleFrequencyGroup_EvenWhenClustersAreDisconnected()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 29, 9, 0, 0, DateTimeKind.Utc), division: null, frequency: "402.0000");
            m1.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m1.AddParticipant("А-1", false, "оператор", 2);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 29, 9, 5, 0, DateTimeKind.Utc), division: null, frequency: "402.0000");
            m2.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m2.AddParticipant("А-2", false, "оператор", 2);

            var m3 = CreateMessage(action, new DateTime(2026, 03, 29, 9, 10, 0, DateTimeKind.Utc), division: null, frequency: "402.0000");
            m3.AddParticipant("РЕЗЕРВ-ЦЕНТР", false, "координатор", 1);
            m3.AddParticipant("Б-1", false, "оператор", 2);

            var m4 = CreateMessage(action, new DateTime(2026, 03, 29, 9, 15, 0, DateTimeKind.Utc), division: null, frequency: "402.0000");
            m4.AddParticipant("РЕЗЕРВ-ЦЕНТР", false, "координатор", 1);
            m4.AddParticipant("Б-2", false, "оператор", 2);

            db.InterceptionMessages.AddRange(m1, m2, m3, m4);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Groups.Should().ContainSingle(
            "в межах однієї частоти має бути одна агрегована група з повним складом учасників");

        var group = result.Groups.Single();
        group.Frequencies.Should().BeEquivalentTo(["402.0000"]);
        group.Members.Should().BeEquivalentTo(["ЦЕНТР", "А-1", "А-2", "РЕЗЕРВ-ЦЕНТР", "Б-1", "Б-2"]);
        group.KeyPersonName.Should().Be("ЦЕНТР", "лідер частоти визначається загальним ранжуванням за зв'язністю та роллю");
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

    [Fact]
    public async Task BuildAsync_CreatesBridge_WhenCenterMeetsForeignBridgeRepresentative()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

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

            var bridgeByCoreRepresentative = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 12, 7, 0, DateTimeKind.Utc),
                division: null,
                frequency: "401.2000");
            bridgeByCoreRepresentative.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
            bridgeByCoreRepresentative.AddParticipant("Б-1", false, "оператор", 2);

            db.InterceptionMessages.AddRange(a1, a2, b1, b2, bridgeByCoreRepresentative);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Groups.Should().HaveCount(2);

        var groupA = result.Groups.Single(x => x.KeyPersonName == "А-ЦЕНТР");
        var groupB = result.Groups.Single(x => x.KeyPersonName == "Б-ЦЕНТР");

        groupA.Bridges.Should().ContainSingle("міжгруповий зв'язок має знаходитися не лише через display-центр, а й через ядро представників групи");
        groupB.Bridges.Should().ContainSingle();

        groupA.Bridges.Single().ContactPersonName.Should().Be("Б-1");
        groupA.Bridges.Single().BridgeFrequency.Should().Be("401.2000");
        groupA.Bridges.Single().Weight.Should().Be(1);

        groupB.Bridges.Single().ContactPersonName.Should().Be("А-ЦЕНТР");
    }

    [Fact]
    public async Task BuildAsync_DoesNotCreateBridge_WhenForeignParticipantIsOutsideBridgeRepresentatives()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var a1 = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 18, 0, 0, DateTimeKind.Utc),
                division: null,
                frequency: "402.0000");
            a1.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
            a1.AddParticipant("А-1", false, "оператор", 2);

            var a2 = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 18, 1, 0, DateTimeKind.Utc),
                division: null,
                frequency: "402.0000");
            a2.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
            a2.AddParticipant("А-2", false, "оператор", 2);

            var b1 = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 18, 2, 0, DateTimeKind.Utc),
                division: null,
                frequency: "145.1000");
            b1.AddParticipant("Б-ЦЕНТР", false, "координатор", 1);
            b1.AddParticipant("Б-1", false, "оператор", 2);

            var b2 = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 18, 3, 0, DateTimeKind.Utc),
                division: null,
                frequency: "145.1000");
            b2.AddParticipant("Б-ЦЕНТР", false, "координатор", 1);
            b2.AddParticipant("Б-2", false, "оператор", 2);

            var b3 = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 18, 4, 0, DateTimeKind.Utc),
                division: null,
                frequency: "145.1000");
            b3.AddParticipant("Б-ЦЕНТР", false, "координатор", 1);
            b3.AddParticipant("Б-3", false, "оператор", 2);

            var b4 = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 18, 5, 0, DateTimeKind.Utc),
                division: null,
                frequency: "145.1000");
            b4.AddParticipant("Б-ЦЕНТР", false, "координатор", 1);
            b4.AddParticipant("Б-4", false, "оператор", 2);

            var bridgeWithNonCoreMember = CreateMessage(
                action,
                new DateTime(2026, 03, 28, 18, 6, 0, DateTimeKind.Utc),
                division: null,
                frequency: "401.2000");
            bridgeWithNonCoreMember.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
            bridgeWithNonCoreMember.AddParticipant("Б-4", false, "оператор", 2);

            db.InterceptionMessages.AddRange(a1, a2, b1, b2, b3, b4, bridgeWithNonCoreMember);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Groups.Should().HaveCount(2);

        var groupA = result.Groups.Single(x => x.KeyPersonName == "А-ЦЕНТР");
        var groupB = result.Groups.Single(x => x.KeyPersonName == "Б-ЦЕНТР");

        groupA.Bridges.Should().BeEmpty("рядовий учасник, який не входить до ядра представників групи, не повинен сам по собі створювати міжгруповий міст");
        groupB.Bridges.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_ReturnsPrimaryActionForGroup_WhenGroupHasDominantAction()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var report = InterceptionAction.Create("доповідь", string.Empty);
            var recon = InterceptionAction.Create("дорозвідка", string.Empty);
            db.InterceptionActions.AddRange(report, recon);

            var m1 = CreateMessage(report, new DateTime(2026, 03, 28, 15, 0, 0, DateTimeKind.Utc), division: null, frequency: "402.0000");
            m1.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m1.AddParticipant("А", false, "оператор", 2);

            var m2 = CreateMessage(report, new DateTime(2026, 03, 28, 15, 5, 0, DateTimeKind.Utc), division: null, frequency: "402.0000");
            m2.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m2.AddParticipant("Б", false, "оператор", 2);

            var m3 = CreateMessage(recon, new DateTime(2026, 03, 28, 15, 10, 0, DateTimeKind.Utc), division: null, frequency: "402.0000");
            m3.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m3.AddParticipant("А", false, "оператор", 2);

            db.InterceptionMessages.AddRange(m1, m2, m3);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        var group = result.Groups.Should().ContainSingle().Subject;
        group.PrimaryAction.Should().Be("доповідь");
        group.TopActions.Should().ContainInOrder("доповідь", "дорозвідка");
    }

    [Fact]
    public async Task BuildAsync_ReturnsTopActionsForGroup_WhenGroupHasMixedActionProfile()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var report = InterceptionAction.Create("доповідь", string.Empty);
            var fireAdjust = InterceptionAction.Create("коригування", string.Empty);
            var recon = InterceptionAction.Create("дорозвідка", string.Empty);
            db.InterceptionActions.AddRange(report, fireAdjust, recon);

            var rows = new[]
            {
                CreateMessage(report, new DateTime(2026, 03, 28, 16, 0, 0, DateTimeKind.Utc), null, "402.0000"),
                CreateMessage(report, new DateTime(2026, 03, 28, 16, 3, 0, DateTimeKind.Utc), null, "402.0000"),
                CreateMessage(fireAdjust, new DateTime(2026, 03, 28, 16, 6, 0, DateTimeKind.Utc), null, "402.0000"),
                CreateMessage(recon, new DateTime(2026, 03, 28, 16, 9, 0, DateTimeKind.Utc), null, "402.0000")
            };

            rows[0].AddParticipant("ЦЕНТР", false, "координатор", 1);
            rows[0].AddParticipant("А", false, "оператор", 2);

            rows[1].AddParticipant("ЦЕНТР", false, "координатор", 1);
            rows[1].AddParticipant("Б", false, "оператор", 2);

            rows[2].AddParticipant("ЦЕНТР", false, "координатор", 1);
            rows[2].AddParticipant("А", false, "оператор", 2);

            rows[3].AddParticipant("ЦЕНТР", false, "координатор", 1);
            rows[3].AddParticipant("Б", false, "оператор", 2);

            db.InterceptionMessages.AddRange(rows);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        var group = result.Groups.Should().ContainSingle().Subject;
        group.TopActions.Should().HaveCount(3);
        group.TopActions[0].Should().Be("доповідь");
        group.TopActions.Should().Contain(["коригування", "дорозвідка"]);
    }

    [Fact]
    public async Task BuildAsync_ReturnsPrimaryActionForBridge_WhenBridgeHasCharacteristicAction()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var report = InterceptionAction.Create("доповідь", string.Empty);
            var coordination = InterceptionAction.Create("координація", string.Empty);
            db.InterceptionActions.AddRange(report, coordination);

            SeedTwoIndependentGroupsWithBridgeActions(db, report, coordination);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        var groupA = result.Groups.Single(x => x.KeyPersonName == "А-ЦЕНТР");
        var groupB = result.Groups.Single(x => x.KeyPersonName == "Б-ЦЕНТР");

        var bridgeFromA = groupA.Bridges.Single(x => x.TargetGroupKey == groupB.GroupKey);
        var bridgeFromB = groupB.Bridges.Single(x => x.TargetGroupKey == groupA.GroupKey);

        bridgeFromA.PrimaryAction.Should().Be("координація");
        bridgeFromA.TopActions.Should().ContainInOrder("координація", "доповідь");

        bridgeFromB.PrimaryAction.Should().Be("координація");
        bridgeFromB.TopActions.Should().ContainInOrder("координація", "доповідь");
    }

    [Fact]
    public async Task BuildAsync_UsesCanonicalDisplayName_ForMergedProfiles()
    {
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var resolvedA = ResolvedParticipant.Create("ШАПКА-1", "seed", role: "координатор", division: "336 мсп");
            var resolvedB = ResolvedParticipant.Create("ШАПКА-2", "seed", role: "координатор", division: "336 мсп");
            db.ResolvedParticipants.AddRange(resolvedA, resolvedB);

            var canonical = CanonicalPerson.Create("ШАПКА");
            canonical.AddMember(resolvedA.Id);
            canonical.AddMember(resolvedB.Id);
            db.CanonicalPersons.Add(canonical);

            var m1 = CreateMessage(action, new DateTime(2026, 03, 29, 8, 0, 0, DateTimeKind.Utc), "336 мсп", "402.0000");
            m1.AddParticipant("ШАПКА-1", false, "координатор", 1);
            m1.AddParticipant("А", false, "оператор", 2);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 29, 8, 5, 0, DateTimeKind.Utc), "336 мсп", "402.0000");
            m2.AddParticipant("ШАПКА-2", false, "координатор", 1);
            m2.AddParticipant("Б", false, "оператор", 2);

            db.InterceptionMessages.AddRange(m1, m2);
            await db.SaveChangesAsync(ct);
        }

        var result = await service.BuildAsync(ct: CancellationToken.None);

        var group = result.Groups.Should().ContainSingle().Subject;
        group.KeyPersonName.Should().Be("ШАПКА");
        group.Members.Should().Contain("ШАПКА");
        group.Members.Should().NotContain("ШАПКА-1");
        group.Members.Should().NotContain("ШАПКА-2");
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
        AppDbContext db,
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

    private static void SeedTwoIndependentGroupsWithBridgeActions(
        AppDbContext db,
        InterceptionAction internalAction,
        InterceptionAction bridgeAction)
    {
        var a1 = CreateMessage(
            internalAction,
            new DateTime(2026, 03, 28, 17, 0, 0, DateTimeKind.Utc),
            division: null,
            frequency: "402.0000");
        a1.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        a1.AddParticipant("А-1", false, "оператор", 2);

        var a2 = CreateMessage(
            internalAction,
            new DateTime(2026, 03, 28, 17, 2, 0, DateTimeKind.Utc),
            division: null,
            frequency: "402.0000");
        a2.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        a2.AddParticipant("А-2", false, "оператор", 2);

        var b1 = CreateMessage(
            internalAction,
            new DateTime(2026, 03, 28, 17, 4, 0, DateTimeKind.Utc),
            division: null,
            frequency: "145.1000");
        b1.AddParticipant("Б-ЦЕНТР", false, "координатор", 1);
        b1.AddParticipant("Б-1", false, "оператор", 2);

        var b2 = CreateMessage(
            internalAction,
            new DateTime(2026, 03, 28, 17, 6, 0, DateTimeKind.Utc),
            division: null,
            frequency: "145.1000");
        b2.AddParticipant("Б-ЦЕНТР", false, "координатор", 1);
        b2.AddParticipant("Б-2", false, "оператор", 2);

        var bridge1 = CreateMessage(
            bridgeAction,
            new DateTime(2026, 03, 28, 17, 8, 0, DateTimeKind.Utc),
            division: null,
            frequency: "401.2000");
        bridge1.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        bridge1.AddParticipant("Б-ЦЕНТР", false, "координатор", 2);

        var bridge2 = CreateMessage(
            bridgeAction,
            new DateTime(2026, 03, 28, 17, 10, 0, DateTimeKind.Utc),
            division: null,
            frequency: "401.2000");
        bridge2.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        bridge2.AddParticipant("Б-ЦЕНТР", false, "координатор", 2);

        var bridge3 = CreateMessage(
            internalAction,
            new DateTime(2026, 03, 28, 17, 12, 0, DateTimeKind.Utc),
            division: null,
            frequency: "401.2000");
        bridge3.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        bridge3.AddParticipant("Б-ЦЕНТР", false, "координатор", 2);

        db.InterceptionMessages.AddRange(a1, a2, b1, b2, bridge1, bridge2, bridge3);
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
