/*//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Interceptions.Services;
using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Interceptions.Services;

public sealed class InterceptionServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static InterceptionService CreateService(IDbContextFactory<AppDbContext> factory)
        => new(factory);

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

    /// <summary>Базова форма — всі поля явно, без with.</summary>
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
                new ParticipantFormDto { Ordinal = 2, Name = "ВОЛГА", IsUnknown = false, Role = "бпла"  },
            ],
            Labels = labels ?? ["тест"],
        };
    }

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ValidForm_PersistsMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        var message = await svc.CreateAsync(MakeForm(action.Id), "operator", ct);

        message.Id.Should().NotBeEmpty();
        message.Frequency.Should().Be("157.0250");
        message.Division.Should().Be("1 мсб 656 мсп");
        message.Participants.Should().HaveCount(2);
        message.Labels.Should().ContainSingle(l => l.NameLabel == "тест");
    }

    [Fact]
    public async Task CreateAsync_UnknownAction_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var act = async () => await svc.CreateAsync(MakeForm(Guid.NewGuid()), "operator", ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*не знайдено*");
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsWithIncludes()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        var created = await svc.CreateAsync(MakeForm(action.Id), "operator", ct);
        var found = await svc.GetByIdAsync(created.Id, ct);

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
        var svc = CreateService(factory);

        var result = await svc.GetByIdAsync(Guid.NewGuid(), ct);

        result.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ChangesFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        var created = await svc.CreateAsync(MakeForm(action.Id), "operator", ct);

        var updateForm = MakeForm(
            actionId: action.Id,
            frequency: "410.1370",
            division: "4 мсб 186 мсп",
            vectorSignal: "новий вектор",
            note: "оновлена нотатка",
            participants:
            [
                new ParticipantFormDto { Ordinal = 1, Name = "КАРАСУК", IsUnknown = false }
            ],
            labels: ["мітка-2"]);

        await svc.UpdateAsync(created.Id, updateForm, ct);
        var updated = await svc.GetByIdAsync(created.Id, ct);

        updated!.Frequency.Should().Be("410.1370");
        updated.Division.Should().Be("4 мсб 186 мсп");
        updated.Participants.Should().ContainSingle(p => p.Name == "КАРАСУК");
        updated.Labels.Should().ContainSingle(l => l.NameLabel == "мітка-2");
    }

    [Fact]
    public async Task UpdateAsync_NotFound_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        var act = async () => await svc.UpdateAsync(Guid.NewGuid(), MakeForm(action.Id), ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*не знайдено*");
    }

    // -------------------------------------------------------------------------
    // DeleteAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RemovesMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        var created = await svc.CreateAsync(MakeForm(action.Id), "operator", ct);
        await svc.DeleteAsync(created.Id, ct);

        var found = await svc.GetByIdAsync(created.Id, ct);
        found.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);

        var act = async () => await svc.DeleteAsync(Guid.NewGuid(), ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // -------------------------------------------------------------------------
    // GetPagedAsync — фільтри
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetPagedAsync_FilterByFrequency_ReturnsOnlyMatching()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await svc.CreateAsync(MakeForm(action.Id, frequency: "157.0250"), "op", ct);
        await svc.CreateAsync(MakeForm(action.Id, frequency: "410.1370"), "op", ct);

        var result = await svc.GetPagedAsync(
            new InterceptionFilter { Frequency = "157.0250" }, ct: ct);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(i => i.Frequency == "157.0250");
    }

    [Fact]
    public async Task GetPagedAsync_FilterByVectorSignal_ReturnsOnlyMatching()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await svc.CreateAsync(
            MakeForm(action.Id, vectorSignal: "степове-олексіївка"), "op", ct);
        await svc.CreateAsync(
            MakeForm(action.Id, vectorSignal: "маліївка-північ"), "op", ct);

        var result = await svc.GetPagedAsync(
            new InterceptionFilter { VectorSignal = "олексіївка" }, ct: ct);

        result.TotalCount.Should().Be(1);
        result.Items[0].VectorSignal.Should().Be("степове-олексіївка");
    }

    [Fact]
    public async Task GetPagedAsync_FilterByParticipant_ReturnsOnlyMatching()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        // Перший запис — ШАПКА + ВОЛГА (з MakeForm за замовчуванням)
        await svc.CreateAsync(MakeForm(action.Id), "op", ct);

        // Другий запис — лише РУБІКОН
        await svc.CreateAsync(
            MakeForm(action.Id, participants:
            [
                new ParticipantFormDto { Ordinal = 1, Name = "РУБІКОН", IsUnknown = false }
            ]), "op", ct);

        var result = await svc.GetPagedAsync(
            new InterceptionFilter { ParticipantName = "ШАПКА" }, ct: ct);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_NoFilter_ReturnsSortedByDateDesc()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await svc.CreateAsync(
            MakeForm(action.Id, observedDate: new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc)),
            "op", ct);
        await svc.CreateAsync(
            MakeForm(action.Id, observedDate: new DateTime(2026, 3, 14, 18, 0, 0, DateTimeKind.Utc)),
            "op", ct);

        var result = await svc.GetPagedAsync(new InterceptionFilter(), ct: ct);

        result.Items[0].ObservedDate
            .Should().BeAfter(result.Items[result.Items.Count - 1].ObservedDate);
    }

    [Fact]
    public async Task GetPagedAsync_Pagination_ReturnsCorrectPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        for (var i = 0; i < 5; i++)
            await svc.CreateAsync(MakeForm(action.Id), "op", ct);

        var page1 = await svc.GetPagedAsync(new InterceptionFilter(), page: 1, pageSize: 2, ct: ct);
        var page2 = await svc.GetPagedAsync(new InterceptionFilter(), page: 2, pageSize: 2, ct: ct);

        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(5);
        page1.TotalPages.Should().Be(3);
    }

    // -------------------------------------------------------------------------
    // GetFrequencyWithDivisionAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetFrequencyWithDivisionAsync_ReturnsMostFrequentDivisionAndVector()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        // "1 мсб" + "вектор-А" зустрічається двічі
        await svc.CreateAsync(
            MakeForm(action.Id, division: "1 мсб", vectorSignal: "вектор-А"), "op", ct);
        await svc.CreateAsync(
            MakeForm(action.Id, division: "1 мсб", vectorSignal: "вектор-А"), "op", ct);
        await svc.CreateAsync(
            MakeForm(action.Id, division: "4 мсб", vectorSignal: "вектор-Б"), "op", ct);

        var suggestions = await svc.GetFrequencyWithDivisionAsync("157", ct: ct);

        suggestions.Should().ContainSingle();
        var s = suggestions[0];
        s.Frequency.Should().Be("157.0250");
        s.Division.Should().Be("1 мсб");
        s.VectorSignal.Should().Be("вектор-А");
    }

    [Fact]
    public async Task GetFrequencyWithDivisionAsync_QueryFilters_ByStartsWith()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await svc.CreateAsync(MakeForm(action.Id, frequency: "157.0250"), "op", ct);
        await svc.CreateAsync(MakeForm(action.Id, frequency: "410.1370"), "op", ct);

        var result = await svc.GetFrequencyWithDivisionAsync("41", ct: ct);

        result.Should().ContainSingle(s => s.Frequency == "410.1370");
    }

    // -------------------------------------------------------------------------
    // GetVectorSignalSuggestionsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetVectorSignalSuggestionsAsync_WithFrequency_ReturnsContextual()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await svc.CreateAsync(
            MakeForm(action.Id, frequency: "157.0250", vectorSignal: "вектор-157"), "op", ct);
        await svc.CreateAsync(
            MakeForm(action.Id, frequency: "410.1370", vectorSignal: "вектор-410"), "op", ct);

        var result = await svc.GetVectorSignalSuggestionsAsync(
            frequency: "157.0250", ct: ct);

        result.Should().ContainSingle();
        result[0].Should().Be("вектор-157");
    }

    [Fact]
    public async Task GetVectorSignalSuggestionsAsync_WithQuery_FiltersContains()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await svc.CreateAsync(
            MakeForm(action.Id, vectorSignal: "степове-олексіївка"), "op", ct);
        await svc.CreateAsync(
            MakeForm(action.Id, vectorSignal: "маліївка-північ"), "op", ct);

        var result = await svc.GetVectorSignalSuggestionsAsync(query: "олексі", ct: ct);

        result.Should().ContainSingle();
        result[0].Should().Be("степове-олексіївка");
    }

    // -------------------------------------------------------------------------
    // GetParticipantSuggestionsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetParticipantSuggestionsAsync_ReturnsKnownParticipants()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await svc.CreateAsync(MakeForm(action.Id), "op", ct); // ШАПКА, ВОЛГА

        var result = await svc.GetParticipantSuggestionsAsync("ШАП", ct: ct);

        result.Should().ContainSingle(p => p.Name == "ШАПКА");
    }

    [Fact]
    public async Task GetParticipantSuggestionsAsync_UnknownParticipants_NotReturned()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var svc = CreateService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await svc.CreateAsync(
            MakeForm(action.Id, participants:
            [
                new ParticipantFormDto { Ordinal = 1, IsUnknown = true, Name = null }
            ]), "op", ct);

        var result = await svc.GetParticipantSuggestionsAsync(ct: ct);

        result.Should().BeEmpty();
    }
}
*/