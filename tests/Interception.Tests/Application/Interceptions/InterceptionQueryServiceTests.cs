//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Interceptions.Services;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Interceptions;

public sealed class InterceptionQueryServiceTests
{
    private static async Task<InterceptionAction> SeedActionAsync(
        IDbContextFactory<AppDbContext> factory,
        string name = "координація дій",
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var action = InterceptionAction.Create(name, "");
        db.InterceptionActions.Add(action);
        await db.SaveChangesAsync(ct);
        return action;
    }

    private static InterceptionFormDto MakeForm(
        Guid actionId,
        string? frequency = "157.0250",
        string? division = "1 мсб 656 мсп",
        string? vectorSignal = "степове-олексіївка",
        string? note = "тестова нотатка",
        DateTime? observedDate = null,
        List<ParticipantFormDto>? participants = null,
        List<string>? labels = null)
    {
        return new InterceptionFormDto
        {
            ObservedDate = observedDate ?? new DateTime(2026, 3, 14, 16, 30, 0, DateTimeKind.Utc),
            Frequency = frequency,
            Division = division,
            VectorSignal = vectorSignal,
            InterceptionActionId = actionId,
            Note = note,
            Participants = participants ??
            [
                new ParticipantFormDto { Ordinal = 1, Name = "ШАПКА", IsUnknown = false, Role = "центр" },
                new ParticipantFormDto { Ordinal = 2, Name = "ВОЛГА", IsUnknown = false, Role = "бпла" },
            ],
            Labels = labels ?? ["тест"],
        };
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsWithIncludes()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = new InterceptionCommandService(factory);
        var query = new InterceptionQueryService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        var created = await command.CreateAsync(MakeForm(action.Id), "operator", ct);
        var found = await query.GetByIdAsync(created.Id, ct);

        found.Should().NotBeNull();
        found!.Participants.Should().HaveCount(2);
        found.Labels.Should().HaveCount(1);
        found.InterceptionAction.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_NotExisting_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var query = new InterceptionQueryService(factory);

        var result = await query.GetByIdAsync(Guid.NewGuid(), ct);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPagedAsync_FilterByFrequency_ReturnsOnlyMatching()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = new InterceptionCommandService(factory);
        var query = new InterceptionQueryService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await command.CreateAsync(MakeForm(action.Id, frequency: "157.0250"), "op", ct);
        await command.CreateAsync(MakeForm(action.Id, frequency: "410.1370"), "op", ct);

        var result = await query.GetPagedAsync(new InterceptionFilter { Frequency = "157.0250" }, ct: ct);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(i => i.Frequency == "157.0250");
    }

    [Fact]
    public async Task GetPagedAsync_FilterByVectorSignal_ReturnsOnlyMatching()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = new InterceptionCommandService(factory);
        var query = new InterceptionQueryService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await command.CreateAsync(MakeForm(action.Id, vectorSignal: "степове-олексіївка"), "op", ct);
        await command.CreateAsync(MakeForm(action.Id, vectorSignal: "маліївка-північ"), "op", ct);

        var result = await query.GetPagedAsync(new InterceptionFilter { VectorSignal = "олексіївка" }, ct: ct);

        result.TotalCount.Should().Be(1);
        result.Items[0].VectorSignal.Should().Be("степове-олексіївка");
    }

    [Fact]
    public async Task GetPagedAsync_FilterByParticipant_ReturnsOnlyMatching()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = new InterceptionCommandService(factory);
        var query = new InterceptionQueryService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await command.CreateAsync(MakeForm(action.Id), "op", ct);
        await command.CreateAsync(
            MakeForm(action.Id, participants:
            [
                new ParticipantFormDto { Ordinal = 1, Name = "РУБІКОН", IsUnknown = false }
            ]), "op", ct);

        var result = await query.GetPagedAsync(new InterceptionFilter { ParticipantName = "ШАПКА" }, ct: ct);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_NoFilter_ReturnsSortedByDateDesc()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = new InterceptionCommandService(factory);
        var query = new InterceptionQueryService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await command.CreateAsync(MakeForm(action.Id, observedDate: new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc)), "op", ct);
        await command.CreateAsync(MakeForm(action.Id, observedDate: new DateTime(2026, 3, 14, 18, 0, 0, DateTimeKind.Utc)), "op", ct);

        var result = await query.GetPagedAsync(new InterceptionFilter(), ct: ct);

        result.Items[0].ObservedDate.Should().BeAfter(result.Items[result.Items.Count - 1].ObservedDate);
    }

    [Fact]
    public async Task GetPagedAsync_Pagination_ReturnsCorrectPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = new InterceptionCommandService(factory);
        var query = new InterceptionQueryService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        for (var i = 0; i < 5; i++)
            await command.CreateAsync(MakeForm(action.Id), "op", ct);

        var page1 = await query.GetPagedAsync(new InterceptionFilter(), page: 1, pageSize: 2, ct: ct);
        var page2 = await query.GetPagedAsync(new InterceptionFilter(), page: 2, pageSize: 2, ct: ct);

        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(5);
        page1.TotalPages.Should().Be(3);
    }
}
