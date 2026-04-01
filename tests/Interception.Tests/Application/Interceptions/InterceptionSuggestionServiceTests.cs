//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.Application.Analytics.Dtos;
using Interception.Application.Interceptions.Dtos;
using Interception.Application.Interceptions.Services;
using Interception.Domain;
using Interception.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application.Interceptions;

public sealed class InterceptionSuggestionServiceTests
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
        List<ParticipantFormDto>? participants = null)
    {
        return new InterceptionFormDto
        {
            ObservedDate = new DateTime(2026, 3, 14, 16, 30, 0, DateTimeKind.Utc),
            Frequency = frequency,
            Division = division,
            VectorSignal = vectorSignal,
            InterceptionActionId = actionId,
            Note = "тестова нотатка",
            Participants = participants ??
            [
                new ParticipantFormDto { Ordinal = 1, Name = "ШАПКА", IsUnknown = false, Role = "центр" },
                new ParticipantFormDto { Ordinal = 2, Name = "ВОЛГА", IsUnknown = false, Role = "бпла" },
            ],
            Labels = ["тест"],
        };
    }

    [Fact]
    public async Task GetFrequencyWithDivisionAsync_ReturnsMostFrequentDivisionAndVector()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = new InterceptionCommandService(factory);
        var suggestion = new InterceptionSuggestionService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await command.CreateAsync(MakeForm(action.Id, division: "1 мсб", vectorSignal: "вектор-А"), "op", ct);
        await command.CreateAsync(MakeForm(action.Id, division: "1 мсб", vectorSignal: "вектор-А"), "op", ct);
        await command.CreateAsync(MakeForm(action.Id, division: "4 мсб", vectorSignal: "вектор-Б"), "op", ct);

        var suggestions = await suggestion.GetFrequencyWithDivisionAsync("157", ct: ct);

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
        var command = new InterceptionCommandService(factory);
        var suggestion = new InterceptionSuggestionService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await command.CreateAsync(MakeForm(action.Id, frequency: "157.0250"), "op", ct);
        await command.CreateAsync(MakeForm(action.Id, frequency: "410.1370"), "op", ct);

        var result = await suggestion.GetFrequencyWithDivisionAsync("41", ct: ct);

        result.Should().ContainSingle(s => s.Frequency == "410.1370");
    }

    [Fact]
    public async Task GetVectorSignalSuggestionsAsync_WithFrequency_ReturnsContextual()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = new InterceptionCommandService(factory);
        var suggestion = new InterceptionSuggestionService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await command.CreateAsync(MakeForm(action.Id, frequency: "157.0250", vectorSignal: "вектор-157"), "op", ct);
        await command.CreateAsync(MakeForm(action.Id, frequency: "410.1370", vectorSignal: "вектор-410"), "op", ct);

        var result = await suggestion.GetVectorSignalSuggestionsAsync(frequency: "157.0250", ct: ct);

        result.Should().ContainSingle();
        result[0].Should().Be("вектор-157");
    }

    [Fact]
    public async Task GetVectorSignalSuggestionsAsync_WithQuery_FiltersContains()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = new InterceptionCommandService(factory);
        var suggestion = new InterceptionSuggestionService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await command.CreateAsync(MakeForm(action.Id, vectorSignal: "степове-олексіївка"), "op", ct);
        await command.CreateAsync(MakeForm(action.Id, vectorSignal: "маліївка-північ"), "op", ct);

        var result = await suggestion.GetVectorSignalSuggestionsAsync(query: "олексі", ct: ct);

        result.Should().ContainSingle();
        result[0].Should().Be("степове-олексіївка");
    }

    [Fact]
    public async Task GetParticipantSuggestionsAsync_ReturnsKnownParticipants()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = new InterceptionCommandService(factory);
        var suggestion = new InterceptionSuggestionService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await command.CreateAsync(MakeForm(action.Id), "op", ct);

        var result = await suggestion.GetParticipantSuggestionsAsync("ШАП", ct: ct);

        result.Should().ContainSingle(p => p.Name == "ШАПКА");
    }

    [Fact]
    public async Task GetParticipantSuggestionsAsync_UnknownParticipants_NotReturned()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var command = new InterceptionCommandService(factory);
        var suggestion = new InterceptionSuggestionService(factory);
        var action = await SeedActionAsync(factory, ct: ct);

        await command.CreateAsync(
            MakeForm(action.Id, participants:
            [
                new ParticipantFormDto { Ordinal = 1, IsUnknown = true, Name = null }
            ]), "op", ct);

        var result = await suggestion.GetParticipantSuggestionsAsync(ct: ct);

        result.Should().BeEmpty();
    }
}
