//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.Application.Analytics.Services;
using Interception.Domain;
using Interception.Domain.Records;
using Interception.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Interception.Tests.Application.Analytics;

public sealed class KnownParticipantSuggestionServiceTests
{
    private static KnownParticipantSuggestionService CreateService(
        IDbContextFactory<AppDbContext> factory,
        PatternRecognitionOptions? options = null)
        => new(factory, Options.Create(options ?? new PatternRecognitionOptions()));

    [Fact]
    public async Task GetKnownSuggestionsAsync_GroupNotFound_ReturnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        var result = await service.GetKnownSuggestionsAsync(Guid.NewGuid(), 3, ct);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetKnownSuggestionsAsync_ExcludesKnownParticipantAlreadyPresentInGroupObservations()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using var db = await factory.CreateDbContextAsync(ct);
        var action = InterceptionAction.Create("доповідь 200", string.Empty);
        db.InterceptionActions.Add(action);

        var baseDate = new DateTime(2026, 03, 25, 10, 00, 00, DateTimeKind.Utc);

        var m1 = CreateMessage(action, baseDate, "142.4500", "БПЛА", "МИРОНОВКА");
        m1.AddLabel("МИРОНОВКА");
        m1.AddLabel("БПЛА");
        var unknown1 = m1.AddParticipant("НВ 1", true, "оператор");
        _ = m1.AddParticipant("ВК", false, "центр");

        var m2 = CreateMessage(action, baseDate.AddMinutes(5), "142.4500", "БПЛА", "МИРОНОВКА");
        m2.AddLabel("МИРОНОВКА");
        m2.AddLabel("БПЛА");
        var unknown2 = m2.AddParticipant("НВ 2", true, "оператор");
        _ = m2.AddParticipant("ВК", false, "центр");

        var vkHistory = CreateMessage(action, baseDate.AddMinutes(10), "142.4500", "БПЛА", "МИРОНОВКА");
        vkHistory.AddLabel("МИРОНОВКА");
        vkHistory.AddLabel("БПЛА");
        _ = vkHistory.AddParticipant("ВК", false, "оператор");

        var grom1 = CreateMessage(action, baseDate.AddMinutes(15), "142.4500", "БПЛА", "МИРОНОВКА");
        grom1.AddLabel("МИРОНОВКА");
        grom1.AddLabel("БПЛА");
        _ = grom1.AddParticipant("ГРОМ", false, "оператор");

        var grom2 = CreateMessage(action, baseDate.AddMinutes(18), "142.4500", "БПЛА", "МИРОНОВКА");
        grom2.AddLabel("МИРОНОВКА");
        grom2.AddLabel("БПЛА");
        _ = grom2.AddParticipant("ГРОМ", false, "оператор");

        db.InterceptionMessages.AddRange(m1, m2, vkHistory, grom1, grom2);
        await db.SaveChangesAsync(ct);

        var group = ParticipantCandidateGroup.Create(
            [
                new ParticipantRef(unknown1.InterceptionMessageId, unknown1.Id, unknown1.Ordinal),
                new ParticipantRef(unknown2.InterceptionMessageId, unknown2.Id, unknown2.Ordinal)
            ],
            0.86,
            new PatternMatchReasons { SameFrequency = true, SameVector = true, SameDivision = true, SharedLabels = true });

        db.ParticipantCandidateGroups.Add(group);
        await db.SaveChangesAsync(ct);

        var result = await service.GetKnownSuggestionsAsync(group.Id, 5, ct);

        result.Should().NotBeEmpty();
        result.Select(x => x.Name).Should().NotContain("ВК");
        result.Select(x => x.Name).Should().Contain("ГРОМ");
    }

    [Fact]
    public async Task GetKnownSuggestionsAsync_ReturnsRankedCandidatesWithReasonsAndRespectsTake()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using var db = await factory.CreateDbContextAsync(ct);
        var action = InterceptionAction.Create("доповідь 200", string.Empty);
        db.InterceptionActions.Add(action);

        var baseDate = new DateTime(2026, 03, 25, 12, 00, 00, DateTimeKind.Utc);

        var m1 = CreateMessage(action, baseDate, "142.4500", "БПЛА", "МИРОНОВКА");
        m1.AddLabel("МИРОНОВКА");
        m1.AddLabel("БПЛА");
        var unknown1 = m1.AddParticipant("НВ 3", true, "оператор");

        var m2 = CreateMessage(action, baseDate.AddMinutes(7), "142.4500", "БПЛА", "МИРОНОВКА");
        m2.AddLabel("МИРОНОВКА");
        m2.AddLabel("БПЛА");
        var unknown2 = m2.AddParticipant("НВ 4", true, "оператор");

        var strong1 = CreateMessage(action, baseDate.AddMinutes(10), "142.4500", "БПЛА", "МИРОНОВКА");
        strong1.AddLabel("МИРОНОВКА");
        strong1.AddLabel("БПЛА");
        _ = strong1.AddParticipant("ГРОМ", false, "оператор");

        var strong2 = CreateMessage(action, baseDate.AddMinutes(20), "142.4500", "БПЛА", "МИРОНОВКА");
        strong2.AddLabel("МИРОНОВКА");
        strong2.AddLabel("БПЛА");
        _ = strong2.AddParticipant("ГРОМ", false, "оператор");

        var weak = CreateMessage(action, baseDate.AddMinutes(15), "142.4500", "ШТУРМ", "ІНШИЙ ВЕКТОР");
        weak.AddLabel("МИРОНОВКА");
        _ = weak.AddParticipant("БОНИК", false, "спостерігач");

        db.InterceptionMessages.AddRange(m1, m2, strong1, strong2, weak);
        await db.SaveChangesAsync(ct);

        var group = ParticipantCandidateGroup.Create(
            [
                new ParticipantRef(unknown1.InterceptionMessageId, unknown1.Id, unknown1.Ordinal),
                new ParticipantRef(unknown2.InterceptionMessageId, unknown2.Id, unknown2.Ordinal)
            ],
            0.88,
            new PatternMatchReasons { SameFrequency = true, SameVector = true, SameDivision = true, SharedLabels = true });

        db.ParticipantCandidateGroups.Add(group);
        await db.SaveChangesAsync(ct);

        var result = await service.GetKnownSuggestionsAsync(group.Id, 1, ct);

        result.Should().HaveCount(1);
        var top = result.Single();
        top.Name.Should().Be("ГРОМ");
        top.Role.Should().Be("оператор");
        top.Division.Should().Be("БПЛА");
        top.SeenCount.Should().Be(2);
        top.MatchScore.Should().BeGreaterThan(0);
        top.CommonLabels.Should().Contain("МИРОНОВКА");
        top.Reasons.SameFrequency.Should().BeTrue();
        top.Reasons.SameVector.Should().BeTrue();
        top.Reasons.SameDivision.Should().BeTrue();
        top.Reasons.SameRole.Should().BeTrue();
        top.Reasons.SharedLabels.Should().BeTrue();
        top.Reasons.PivotIntersection.Should().BeTrue();
    }

    [Fact]
    public async Task GetKnownSuggestionsAsync_TimeOnlyOverlap_DoesNotReturnCandidate()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using var db = await factory.CreateDbContextAsync(ct);
        var action = InterceptionAction.Create("доповідь 200", string.Empty);
        db.InterceptionActions.Add(action);

        var baseDate = new DateTime(2026, 03, 25, 14, 00, 00, DateTimeKind.Utc);

        var m1 = CreateMessage(action, baseDate, "142.4500", "БПЛА", "МИРОНОВКА");
        m1.AddLabel("МИРОНОВКА");
        var unknown1 = m1.AddParticipant("НВ 5", true, "оператор");

        var m2 = CreateMessage(action, baseDate.AddMinutes(3), "142.4500", "БПЛА", "МИРОНОВКА");
        m2.AddLabel("МИРОНОВКА");
        var unknown2 = m2.AddParticipant("НВ 6", true, "оператор");

        var timeOnly = CreateMessage(action, baseDate.AddMinutes(5), "999.9999", "ІНШИЙ ПІДРОЗДІЛ", "ІНШИЙ ВЕКТОР");
        timeOnly.AddLabel("ЧУЖИЙ КОНТЕКСТ");
        _ = timeOnly.AddParticipant("СОНЦЕ", false, "черговий");

        db.InterceptionMessages.AddRange(m1, m2, timeOnly);
        await db.SaveChangesAsync(ct);

        var group = ParticipantCandidateGroup.Create(
            [
                new ParticipantRef(unknown1.InterceptionMessageId, unknown1.Id, unknown1.Ordinal),
                new ParticipantRef(unknown2.InterceptionMessageId, unknown2.Id, unknown2.Ordinal)
            ],
            0.80,
            new PatternMatchReasons { SameFrequency = true, SameVector = true });

        db.ParticipantCandidateGroups.Add(group);
        await db.SaveChangesAsync(ct);

        var result = await service.GetKnownSuggestionsAsync(group.Id, 3, ct);

        result.Should().BeEmpty();
    }

    private static InterceptionMessage CreateMessage(
        InterceptionAction action,
        DateTime observedDate,
        string? frequency,
        string? division,
        string? vectorSignal)
        => InterceptionMessage.Create(
            observedDate,
            frequency,
            division,
            vectorSignal,
            action,
            note: null,
            createdBy: "tester");
}
