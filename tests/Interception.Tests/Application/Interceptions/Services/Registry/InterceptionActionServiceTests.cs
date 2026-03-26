//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Interceptions.Services.Registry;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Interceptions.Services.Registry;

public sealed class InterceptionActionServiceTests
{
    private static InterceptionActionService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ValidName_PersistsAction()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var result = await svc.CreateAsync("доповідь 200", "опис", ct);

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("доповідь 200");
        result.Description.Should().Be("опис");

        var all = await svc.GetAllAsync(ct);
        all.Should().ContainSingle(a => a.Name == "доповідь 200");
    }

    [Fact]
    public async Task CreateAsync_TrimsWhitespace()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var result = await svc.CreateAsync("  доповідь 300  ", "", ct);

        result.Name.Should().Be("доповідь 300");
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await svc.CreateAsync("наказ на ураження", "", ct);

        var act = async () => await svc.CreateAsync("наказ на ураження", "інший опис", ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*наказ на ураження*");
    }

    [Fact]
    public async Task CreateAsync_EmptyName_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var act = async () => await svc.CreateAsync("", "", ct);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ChangesNameAndDescription()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var created = await svc.CreateAsync("стара назва", "старий опис", ct);
        var updated = await svc.UpdateAsync(created.Id, "нова назва", "новий опис", ct);

        updated.Name.Should().Be("нова назва");
        updated.Description.Should().Be("новий опис");

        var all = await svc.GetAllAsync(ct);
        all.Should().NotContain(a => a.Name == "стара назва");
        all.Should().ContainSingle(a => a.Name == "нова назва");
    }

    [Fact]
    public async Task UpdateAsync_NameConflictWithOther_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await svc.CreateAsync("дія А", "", ct);
        var b = await svc.CreateAsync("дія Б", "", ct);

        var act = async () => await svc.UpdateAsync(b.Id, "дія А", "", ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*дія А*");
    }

    [Fact]
    public async Task UpdateAsync_SameNameSelf_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var created = await svc.CreateAsync("координація дій", "", ct);
        var updated = await svc.UpdateAsync(created.Id, "координація дій", "новий опис", ct);

        updated.Name.Should().Be("координація дій");
        updated.Description.Should().Be("новий опис");
    }

    [Fact]
    public async Task UpdateAsync_NotFound_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var act = async () => await svc.UpdateAsync(Guid.NewGuid(), "будь-що", "", ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*не знайдено*");
    }

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_ReturnsSortedByName()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await svc.CreateAsync("інше", "", ct);
        await svc.CreateAsync("доповідь 200", "", ct);
        await svc.CreateAsync("запит по зв'язку", "", ct);

        var all = await svc.GetAllAsync(ct);

        all.Select(a => a.Name).Should().BeInAscendingOrder();
    }

    // -------------------------------------------------------------------------
    // SeedFromListAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SeedFromListAsync_AddsNewSkipsExisting()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        await svc.CreateAsync("доповідь 200", "", ct);

        var added = await svc.SeedFromListAsync(
            ["доповідь 200", "доповідь 300", "інше"], ct);

        added.Should().Be(2);

        var all = await svc.GetAllAsync(ct);
        all.Should().HaveCount(3);
    }

    [Fact]
    public async Task SeedFromListAsync_EmptyList_ReturnsZero()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var added = await svc.SeedFromListAsync([], ct);

        added.Should().Be(0);
    }

    [Fact]
    public async Task SeedFromListAsync_SkipsDuplicatesWithinList()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var added = await svc.SeedFromListAsync(
            ["координація дій", "координація дій", "інше"], ct);

        added.Should().Be(2);
    }
}
