//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Analytics.Services;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Interceptions.Services;
using Interception.UI.Domain.Entities;
using Interception.UI.Domain.Enums;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Analytics;

/// <summary>
/// TDD-тести для write-side побудови snapshot-ів топології.
/// </summary>
public sealed class TopologySnapshotBuilderTests
{
    private static TopologySnapshotBuilder CreateBuilder(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task GetStateAsync_NoRuns_ReturnsMissingState()
    {
        var factory = TestDbFactory.CreateFactory();
        var builder = CreateBuilder(factory);

        var state = await builder.GetStateAsync(ct: CancellationToken.None);

        state.HasSnapshot.Should().BeFalse();
        state.IsStale.Should().BeTrue();
        state.IsBuilding.Should().BeFalse();
        state.Status.Should().Be("missing");
        state.GroupCount.Should().Be(0);
        state.LastCompletedAt.Should().BeNull();
        state.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task RebuildAsync_CreatesCompletedSnapshotRun_WhenCommunicationGroupsExist()
    {
        var factory = TestDbFactory.CreateFactory();
        var builder = CreateBuilder(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var report = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(report);

            SeedTwoIndependentGroupsWithBridge(db, report);
            await db.SaveChangesAsync(ct);
        }

        var result = await builder.RebuildAsync(ct: CancellationToken.None);

        await using var assertDb = await factory.CreateDbContextAsync(ct);

        var run = await assertDb.TopologySnapshotRuns
            .Include(x => x.Groups)
            .ThenInclude(x => x.Bridges)
            .FirstAsync(x => x.Id == result.RunId, ct);

        run.Status.Should().Be(TopologySnapshotRunStatus.Completed);
        run.IsStale.Should().BeFalse();
        run.GroupCount.Should().Be(2);
        run.CompletedAt.Should().NotBeNull();
        run.Groups.Should().HaveCount(2);
        run.Groups.Should().OnlyContain(x => x.Bridges.Count == 1);
    }

    [Fact]
    public async Task RebuildAsync_PersistsMembersFrequenciesAndActions_ForBuiltGroup()
    {
        var factory = TestDbFactory.CreateFactory();
        var builder = CreateBuilder(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var report = InterceptionAction.Create("доповідь", string.Empty);
            var recon = InterceptionAction.Create("дорозвідка", string.Empty);
            db.InterceptionActions.AddRange(report, recon);

            var m1 = CreateMessage(report, new DateTime(2026, 03, 28, 10, 0, 0, DateTimeKind.Utc), null, "402.0000");
            m1.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m1.AddParticipant("А", false, "оператор", 2);

            var m2 = CreateMessage(report, new DateTime(2026, 03, 28, 10, 5, 0, DateTimeKind.Utc), null, "402.0000");
            m2.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m2.AddParticipant("Б", false, "оператор", 2);

            var m3 = CreateMessage(recon, new DateTime(2026, 03, 28, 10, 10, 0, DateTimeKind.Utc), null, "402.0000");
            m3.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m3.AddParticipant("А", false, "оператор", 2);

            db.InterceptionMessages.AddRange(m1, m2, m3);
            await db.SaveChangesAsync(ct);
        }

        var rebuild = await builder.RebuildAsync(ct: CancellationToken.None);

        await using var assertDb = await factory.CreateDbContextAsync(ct);
        var run = await assertDb.TopologySnapshotRuns
            .Include(x => x.Groups)
                .ThenInclude(x => x.Frequencies)
            .Include(x => x.Groups)
                .ThenInclude(x => x.Members)
            .Include(x => x.Groups)
                .ThenInclude(x => x.Actions)
            .FirstAsync(x => x.Id == rebuild.RunId, ct);

        var group = run.Groups.Should().ContainSingle().Subject;
        group.KeyPersonName.Should().Be("ЦЕНТР");
        group.KeyPersonRole.Should().Be("координатор");
        group.Frequencies.Select(x => x.Frequency).Should().BeEquivalentTo(["402.0000"]);
        group.Members.Select(x => x.Name).Should().BeEquivalentTo(["ЦЕНТР", "А", "Б"]);
        group.Actions.OrderBy(x => x.SortOrder).Select(x => x.Name).Should().ContainInOrder("доповідь", "дорозвідка");
        group.Actions.Should().ContainSingle(x => x.IsPrimary && x.Name == "доповідь");
    }

    [Fact]
    public async Task GetStateAsync_ReturnsCompletedState_AfterSuccessfulRebuild()
    {
        var factory = TestDbFactory.CreateFactory();
        var builder = CreateBuilder(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var report = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(report);

            var m1 = CreateMessage(report, new DateTime(2026, 03, 28, 9, 0, 0, DateTimeKind.Utc), null, "402.0000");
            m1.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m1.AddParticipant("А", false, "оператор", 2);

            var m2 = CreateMessage(report, new DateTime(2026, 03, 28, 9, 5, 0, DateTimeKind.Utc), null, "402.0000");
            m2.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m2.AddParticipant("Б", false, "оператор", 2);

            db.InterceptionMessages.AddRange(m1, m2);
            await db.SaveChangesAsync(ct);
        }

        await builder.RebuildAsync(ct: CancellationToken.None);
        var state = await builder.GetStateAsync(ct: CancellationToken.None);

        state.HasSnapshot.Should().BeTrue();
        state.IsStale.Should().BeFalse();
        state.IsBuilding.Should().BeFalse();
        state.Status.Should().Be("completed");
        state.GroupCount.Should().Be(1);
        state.LastCompletedAt.Should().NotBeNull();
        state.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task GetStateAsync_ReturnsStaleAfterInterceptionCreateAsync_WhenCompletedSnapshotAlreadyExists()
    {
        var factory = TestDbFactory.CreateFactory();
        var builder = CreateBuilder(factory);
        var commandService = new InterceptionCommandService(factory);
        var ct = TestContext.Current.CancellationToken;
        Guid actionId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var report = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(report);
            await db.SaveChangesAsync(ct);
            actionId = report.Id;

            var m1 = CreateMessage(report, new DateTime(2026, 03, 28, 8, 0, 0, DateTimeKind.Utc), null, "402.0000");
            m1.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m1.AddParticipant("А", false, "оператор", 2);

            var m2 = CreateMessage(report, new DateTime(2026, 03, 28, 8, 5, 0, DateTimeKind.Utc), null, "402.0000");
            m2.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m2.AddParticipant("Б", false, "оператор", 2);

            db.InterceptionMessages.AddRange(m1, m2);
            await db.SaveChangesAsync(ct);
        }

        await builder.RebuildAsync(ct: CancellationToken.None);

        var form = new InterceptionFormDto
        {
            ObservedDate = new DateTime(2026, 03, 28, 8, 10, 0, DateTimeKind.Utc),
            Frequency = "403.0000",
            Division = null,
            VectorSignal = "р-н Шевченко",
            InterceptionActionId = actionId,
            Note = "stale-check",
            Participants =
            [
                new() { Name = "НОВИЙ-ЦЕНТР", Role = "координатор", IsUnknown = false, Ordinal = 1 },
                new() { Name = "НОВИЙ-1", Role = "оператор", IsUnknown = false, Ordinal = 2 }
            ]
        };

        await commandService.CreateAsync(form, "tester", ct);

        var state = await builder.GetStateAsync(ct: CancellationToken.None);
        state.HasSnapshot.Should().BeTrue();
        state.IsStale.Should().BeTrue();
        state.Status.Should().Be("completed");

        await using var assertDb = await factory.CreateDbContextAsync(ct);
        var latestCompleted = await assertDb.TopologySnapshotRuns
            .OrderByDescending(x => x.CompletedAt)
            .FirstAsync(x => x.Status == TopologySnapshotRunStatus.Completed, ct);

        latestCompleted.IsStale.Should().BeTrue();
    }

    [Fact]
    public async Task RebuildAsync_UsesNormalizedPeriod_AndLinkMapServiceReadsSameSnapshot()
    {
        var factory = TestDbFactory.CreateFactory();
        var builder = CreateBuilder(factory);
        var service = new LinkMapService(factory);
        var ct = TestContext.Current.CancellationToken;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var report = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(report);

            var m1 = CreateMessage(report, new DateTime(2026, 03, 28, 11, 0, 0, DateTimeKind.Utc), null, "402.0000");
            m1.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m1.AddParticipant("А", false, "оператор", 2);

            var m2 = CreateMessage(report, new DateTime(2026, 03, 28, 11, 5, 0, DateTimeKind.Utc), null, "402.0000");
            m2.AddParticipant("ЦЕНТР", false, "координатор", 1);
            m2.AddParticipant("Б", false, "оператор", 2);

            db.InterceptionMessages.AddRange(m1, m2);
            await db.SaveChangesAsync(ct);
        }

        var dateFromUtc = new DateTime(2026, 03, 28, 0, 0, 0, DateTimeKind.Utc);
        var dateToUtc = new DateTime(2026, 03, 29, 0, 0, 0, DateTimeKind.Utc);

        await builder.RebuildAsync(dateFromUtc, dateToUtc, CancellationToken.None);
        var result = await service.BuildAsync(dateFromUtc, dateToUtc, CancellationToken.None);

        result.Groups.Should().ContainSingle();
        result.Groups.Single().KeyPersonName.Should().Be("ЦЕНТР");
    }

    [Fact]
    public async Task RebuildAsync_StoresCanonicalDisplayName_ForMergedProfiles()
    {
        var factory = TestDbFactory.CreateFactory();
        var builder = CreateBuilder(factory);
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

            var m1 = CreateMessage(action, new DateTime(2026, 03, 29, 10, 0, 0, DateTimeKind.Utc), "336 мсп", "402.0000");
            m1.AddParticipant("ШАПКА-1", false, "координатор", 1);
            m1.AddParticipant("А", false, "оператор", 2);

            var m2 = CreateMessage(action, new DateTime(2026, 03, 29, 10, 5, 0, DateTimeKind.Utc), "336 мсп", "402.0000");
            m2.AddParticipant("ШАПКА-2", false, "координатор", 1);
            m2.AddParticipant("Б", false, "оператор", 2);

            db.InterceptionMessages.AddRange(m1, m2);
            await db.SaveChangesAsync(ct);
        }

        var rebuild = await builder.RebuildAsync(ct: CancellationToken.None);

        await using var assertDb = await factory.CreateDbContextAsync(ct);
        var run = await assertDb.TopologySnapshotRuns
            .Include(x => x.Groups)
            .ThenInclude(x => x.Members)
            .FirstAsync(x => x.Id == rebuild.RunId, ct);

        var group = run.Groups.Should().ContainSingle().Subject;
        group.KeyPersonName.Should().Be("ШАПКА");
        group.Members.Should().Contain(x => x.Name == "ШАПКА");
        group.Members.Should().NotContain(x => x.Name == "ШАПКА-1");
        group.Members.Should().NotContain(x => x.Name == "ШАПКА-2");
    }

    private static void SeedTwoIndependentGroupsWithBridge(AppDbContext db, InterceptionAction action)
    {
        var a1 = CreateMessage(action, new DateTime(2026, 03, 28, 12, 0, 0, DateTimeKind.Utc), null, "402.0000");
        a1.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        a1.AddParticipant("А-1", false, "оператор", 2);

        var a2 = CreateMessage(action, new DateTime(2026, 03, 28, 12, 1, 0, DateTimeKind.Utc), null, "402.0000");
        a2.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        a2.AddParticipant("А-2", false, "оператор", 2);

        var b1 = CreateMessage(action, new DateTime(2026, 03, 28, 12, 2, 0, DateTimeKind.Utc), null, "145.1000");
        b1.AddParticipant("Б-ЦЕНТР", false, "координатор", 1);
        b1.AddParticipant("Б-1", false, "оператор", 2);

        var b2 = CreateMessage(action, new DateTime(2026, 03, 28, 12, 3, 0, DateTimeKind.Utc), null, "145.1000");
        b2.AddParticipant("Б-ЦЕНТР", false, "координатор", 1);
        b2.AddParticipant("Б-2", false, "оператор", 2);

        var bridge1 = CreateMessage(action, new DateTime(2026, 03, 28, 12, 4, 0, DateTimeKind.Utc), null, "401.2000");
        bridge1.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        bridge1.AddParticipant("Б-ЦЕНТР", false, "координатор", 2);

        var bridge2 = CreateMessage(action, new DateTime(2026, 03, 28, 12, 5, 0, DateTimeKind.Utc), null, "401.2000");
        bridge2.AddParticipant("А-ЦЕНТР", false, "координатор", 1);
        bridge2.AddParticipant("Б-ЦЕНТР", false, "координатор", 2);

        db.InterceptionMessages.AddRange(a1, a2, b1, b2, bridge1, bridge2);
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
