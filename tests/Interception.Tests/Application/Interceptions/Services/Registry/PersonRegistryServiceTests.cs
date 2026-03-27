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
    public async Task GetAllAsync_ReturnsResolvedParticipantsSortedByName()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ResolvedParticipants.Add(ResolvedParticipant.Create("ШТОРМ", "seed"));
            db.ResolvedParticipants.Add(ResolvedParticipant.Create("БОНИК", "seed"));
            db.ResolvedParticipants.Add(ResolvedParticipant.Create("ГРОМ", "seed"));
            await db.SaveChangesAsync(ct);
        }

        var all = await svc.GetAllAsync(ct);

        all.Select(x => x.Name).Should().Equal("БОНИК", "ГРОМ", "ШТОРМ");
    }

    [Fact]
    public async Task GetAllAsync_AllowsTwoPersonsWithSameName()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            db.ResolvedParticipants.Add(ResolvedParticipant.Create("МАДЖЕСТИК", "seed", "оператор", "1 мсб"));
            db.ResolvedParticipants.Add(ResolvedParticipant.Create("МАДЖЕСТИК", "seed", "навідник", "2 мсб"));
            await db.SaveChangesAsync(ct);
        }

        var all = await svc.GetAllAsync(ct);

        all.Should().HaveCount(2);
        all.Should().OnlyContain(x => x.Name == "МАДЖЕСТИК");
        all.Select(x => x.Division).Should().BeEquivalentTo(["1 мсб", "2 мсб"]);
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ChangesNameRoleAndDivision()
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

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var result = await verifyDb.ResolvedParticipants.SingleAsync(r => r.Id == id, ct);
        result.Name.Should().Be("ШТОРМ");
        result.Role.Should().Be("старший");
        result.Division.Should().Be("РЕР");
    }

    [Fact]
    public async Task UpdateAsync_AllowsDuplicateNameWithOtherPerson()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        Guid id;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var a = ResolvedParticipant.Create("МАДЖЕСТИК", "seed", "оператор", "1 мсб");
            var b = ResolvedParticipant.Create("ШТОРМ", "seed", "навідник", "2 мсб");
            id = b.Id;
            db.ResolvedParticipants.AddRange(a, b);
            await db.SaveChangesAsync(ct);
        }

        var dto = await svc.UpdateAsync(
            id,
            new PersonRegistryUpdateDto
            {
                Name = "МАДЖЕСТИК",
                Role = "старший",
                Division = "2 мсб"
            },
            ct);

        dto.Name.Should().Be("МАДЖЕСТИК");
        dto.Role.Should().Be("старший");
        dto.Division.Should().Be("2 мсб");

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var all = await verifyDb.ResolvedParticipants
            .Where(x => x.Name == "МАДЖЕСТИК")
            .OrderBy(x => x.Division)
            .ToListAsync(ct);

        all.Should().HaveCount(2);
        all.Select(x => x.Division).Should().Equal("1 мсб", "2 мсб");
    }
}
