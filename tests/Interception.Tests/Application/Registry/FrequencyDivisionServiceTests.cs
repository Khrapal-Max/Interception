//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.Application.Registry.Services;
using Interception.Domain;
using Interception.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Registry;

public sealed class FrequencyDivisionServiceTests
{
    private static FrequencyDivisionService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task GetAllAsync_ReturnsDistinctFrequenciesWithDominantDivision()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            db.InterceptionMessages.AddRange(
                CreateMessage(action, new DateTime(2026, 03, 27, 8, 0, 0, DateTimeKind.Utc), frequency: "402.0000", division: "656 мсп"),
                CreateMessage(action, new DateTime(2026, 03, 27, 8, 5, 0, DateTimeKind.Utc), frequency: "402.0000", division: "656 мсп"),
                CreateMessage(action, new DateTime(2026, 03, 27, 8, 10, 0, DateTimeKind.Utc), frequency: "402.0000", division: null),
                CreateMessage(action, new DateTime(2026, 03, 27, 8, 15, 0, DateTimeKind.Utc), frequency: "145.1000", division: "336 мсп"));

            await db.SaveChangesAsync(ct);
        }

        var items = await service.GetAllAsync(ct: ct);

        items.Should().ContainSingle(x => x.Frequency == "402.0000" && x.Division == "656 мсп");
        items.Should().ContainSingle(x => x.Frequency == "145.1000" && x.Division == "336 мсп");
    }

    [Fact]
    public async Task CorrectDivisionAsync_UpdatesEmptyAndUnknownDivisionValuesForFrequency()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            db.InterceptionMessages.AddRange(
                CreateMessage(action, new DateTime(2026, 03, 27, 9, 0, 0, DateTimeKind.Utc), frequency: "402.0000", division: null),
                CreateMessage(action, new DateTime(2026, 03, 27, 9, 5, 0, DateTimeKind.Utc), frequency: "402.0000", division: "НВ підрозділ"),
                CreateMessage(action, new DateTime(2026, 03, 27, 9, 10, 0, DateTimeKind.Utc), frequency: "402.0000", division: "656 мсп"));

            await db.SaveChangesAsync(ct);
        }

        await service.CorrectDivisionAsync("402.0000", "656 мсп", ct: ct);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var divisions = await db.InterceptionMessages
                .AsNoTracking()
                .Where(x => x.Frequency == "402.0000")
                .OrderBy(x => x.ObservedDate)
                .Select(x => x.Division)
                .ToListAsync(ct);

            divisions.Should().Equal("656 мсп", "656 мсп", "656 мсп");
        }
    }

    [Fact]
    public async Task CorrectDivisionAsync_DoesNotChangeOtherFrequencies()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            db.InterceptionMessages.AddRange(
                CreateMessage(action, new DateTime(2026, 03, 27, 10, 0, 0, DateTimeKind.Utc), frequency: "402.0000", division: null),
                CreateMessage(action, new DateTime(2026, 03, 27, 10, 5, 0, DateTimeKind.Utc), frequency: "145.1000", division: "336 мсп"));

            await db.SaveChangesAsync(ct);
        }

        await service.CorrectDivisionAsync("402.0000", "656 мсп", ct: ct);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var frequency402 = await db.InterceptionMessages
                .AsNoTracking()
                .SingleAsync(x => x.Frequency == "402.0000", ct);

            var frequency145 = await db.InterceptionMessages
                .AsNoTracking()
                .SingleAsync(x => x.Frequency == "145.1000", ct);

            frequency402.Division.Should().Be("656 мсп");
            frequency145.Division.Should().Be("336 мсп");
        }
    }

    [Fact]
    public async Task GetAllAsync_AfterCorrection_ShowsCorrectedDivision()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            db.InterceptionMessages.AddRange(
                CreateMessage(action, new DateTime(2026, 03, 27, 11, 0, 0, DateTimeKind.Utc), frequency: "402.0000", division: null),
                CreateMessage(action, new DateTime(2026, 03, 27, 11, 5, 0, DateTimeKind.Utc), frequency: "402.0000", division: "НВ підрозділ"));

            await db.SaveChangesAsync(ct);
        }

        await service.CorrectDivisionAsync("402.0000", "656 мсп", ct: ct);

        var items = await service.GetAllAsync(ct: ct);

        items.Should().ContainSingle(x => x.Frequency == "402.0000" && x.Division == "656 мсп");
    }

    [Fact]
    public async Task CorrectDivisionAsync_ThrowsWhenFrequencyIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        var act = () => service.CorrectDivisionAsync(string.Empty, "656 мсп", ct: ct);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static InterceptionMessage CreateMessage(
        InterceptionAction action,
        DateTime observedDate,
        string frequency,
        string? division)
        => InterceptionMessage.Create(
            observedDate,
            frequency,
            division,
            vectorSignal: null,
            interceptionAction: action,
            note: null,
            createdBy: "test");
}
