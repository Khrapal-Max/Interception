//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Registry.Dtos;
using Interception.UI.Application.Registry.Services;
using Interception.UI.Domain.Interceptions;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Registry;

/// <summary>
/// Тести для сервісу реєстру осіб.
/// Покривають приховування partial/observed дубля після confirm
/// і перевикористання confirmed context без створення дубліката.
/// </summary>
public sealed class PersonRegistryServiceTests
{
    private static PersonRegistryService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    [Fact]
    public async Task GetAllAsync_HidesObservedRow_WhenMatchingConfirmedContextExists()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message = InterceptionMessage.Create(
                new DateTime(2026, 04, 10, 12, 00, 00, DateTimeKind.Utc),
                "402.0000",
                "336 мсп",
                "р-н Шевченко",
                action,
                note: null,
                createdBy: "seed");

            message.AddParticipant("КЛИМ", isUnknown: false, role: "водитель", ordinal: 1);
            db.InterceptionMessages.Add(message);

            var resolved = ResolvedParticipant.Create("КЛИМ", "registry", "водитель", "336 мсп");
            db.ResolvedParticipants.Add(resolved);
            db.Entry(resolved).Property("Frequency").CurrentValue = "402.0000";

            await db.SaveChangesAsync(ct);
        }

        var rows = await service.GetAllAsync(ct);

        rows.Should().ContainSingle();
        rows[0].IsConfirmed.Should().BeTrue();
        rows[0].Name.Should().Be("КЛИМ");
        rows[0].Frequency.Should().Be("402.0000");
        rows[0].Division.Should().Be("336 мсп");
    }

    [Fact]
    public async Task GetAllAsync_KeepsObservedRow_WhenConfirmedHasDifferentFrequency()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message = InterceptionMessage.Create(
                new DateTime(2026, 04, 10, 12, 00, 00, DateTimeKind.Utc),
                "402.0000",
                "336 мсп",
                "р-н Шевченко",
                action,
                note: null,
                createdBy: "seed");

            message.AddParticipant("КЛИМ", isUnknown: false, role: "водитель", ordinal: 1);
            db.InterceptionMessages.Add(message);

            var resolved = ResolvedParticipant.Create("КЛИМ", "registry", "водитель", "336 мсп");
            db.ResolvedParticipants.Add(resolved);
            db.Entry(resolved).Property("Frequency").CurrentValue = "145.1000";

            await db.SaveChangesAsync(ct);
        }

        var rows = await service.GetAllAsync(ct);

        rows.Should().HaveCount(2);
        rows.Should().Contain(x => x.IsConfirmed && x.Name == "КЛИМ" && x.Frequency == "145.1000");
        rows.Should().Contain(x => !x.IsConfirmed && x.Name == "КЛИМ" && x.Frequency == "402.0000");
    }

    [Fact]
    public async Task UpdateAsync_ForObservedRowWithSameContext_ReusesExistingConfirmedWithoutDuplicate()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        Guid observedParticipantId;
        Guid existingResolvedId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message = InterceptionMessage.Create(
                new DateTime(2026, 04, 10, 12, 00, 00, DateTimeKind.Utc),
                "402.0000",
                "336 мсп",
                "р-н Шевченко",
                action,
                note: null,
                createdBy: "seed");

            var participant = message.AddParticipant("КЛИМ", isUnknown: false, role: "водитель", ordinal: 1);
            observedParticipantId = participant.Id;
            db.InterceptionMessages.Add(message);

            var resolved = ResolvedParticipant.Create("КЛИМ", "registry", "старий", "336 мсп");
            existingResolvedId = resolved.Id;
            db.ResolvedParticipants.Add(resolved);
            db.Entry(resolved).Property("Frequency").CurrentValue = "402.0000";

            await db.SaveChangesAsync(ct);
        }

        var result = await service.UpdateAsync(observedParticipantId, new PersonRegistryUpdateDto
        {
            Name = "КЛИМ",
            Frequency = "402.0000",
            Role = "водитель",
            Division = "336 мсп"
        }, ct);

        result.Id.Should().Be(existingResolvedId);
        result.IsConfirmed.Should().BeTrue();
        result.Frequency.Should().Be("402.0000");
        result.Role.Should().Be("водитель");

        await using (var verifyDb = await factory.CreateDbContextAsync(ct))
        {
            (await verifyDb.ResolvedParticipants.CountAsync(ct)).Should().Be(1);

            var persisted = await verifyDb.ResolvedParticipants.SingleAsync(ct);
            persisted.Id.Should().Be(existingResolvedId);
            persisted.Name.Should().Be("КЛИМ");
            persisted.Role.Should().Be("водитель");
            persisted.Division.Should().Be("336 мсп");

            verifyDb.Entry(persisted).Property<string?>("Frequency").CurrentValue.Should().Be("402.0000");
        }

        var rows = await service.GetAllAsync(ct);
        rows.Should().ContainSingle(x => x.IsConfirmed && x.Name == "КЛИМ" && x.Frequency == "402.0000");
        rows.Should().NotContain(x => !x.IsConfirmed && x.Name == "КЛИМ" && x.Frequency == "402.0000");
    }

    [Fact]
    public async Task UpdateAsync_ForObservedRowWithDifferentContext_CreatesNewConfirmedRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        Guid observedParticipantId;
        Guid existingResolvedId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message = InterceptionMessage.Create(
                new DateTime(2026, 04, 10, 12, 00, 00, DateTimeKind.Utc),
                "402.0000",
                "336 мсп",
                "р-н Шевченко",
                action,
                note: null,
                createdBy: "seed");

            var participant = message.AddParticipant("КЛИМ", isUnknown: false, role: "водитель", ordinal: 1);
            observedParticipantId = participant.Id;
            db.InterceptionMessages.Add(message);

            var resolved = ResolvedParticipant.Create("КЛИМ", "registry", "водитель", "336 мсп");
            existingResolvedId = resolved.Id;
            db.ResolvedParticipants.Add(resolved);
            db.Entry(resolved).Property("Frequency").CurrentValue = "145.1000";

            await db.SaveChangesAsync(ct);
        }

        var result = await service.UpdateAsync(observedParticipantId, new PersonRegistryUpdateDto
        {
            Name = "КЛИМ",
            Frequency = "402.0000",
            Role = "водитель",
            Division = "336 мсп"
        }, ct);

        result.Id.Should().NotBe(existingResolvedId);
        result.IsConfirmed.Should().BeTrue();
        result.Frequency.Should().Be("402.0000");

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        (await verifyDb.ResolvedParticipants.CountAsync(ct)).Should().Be(2);
    }

    private sealed class TestDbFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        private TestDbFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public static IDbContextFactory<AppDbContext> CreateFactory()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"person-registry-tests-{Guid.NewGuid()}")
                .EnableSensitiveDataLogging()
                .Options;

            return new TestDbFactory(options);
        }

        public AppDbContext CreateDbContext()
            => new(_options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
