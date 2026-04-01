//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.Application.Analytics.Services;
using Interception.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Interception.Tests.Application.Analytics;

public sealed class ParticipantCandidateAnalysisServiceTests
{
    private static ParticipantCandidateAnalysisService CreateService(
        IDbContextFactory<UI.Infrastructure.AppDbContext> factory,
        PatternRecognitionOptions? options = null)
    {
        options ??= new PatternRecognitionOptions();
        var knownService = new KnownParticipantSuggestionService(factory, Options.Create(options));
        return new ParticipantCandidateAnalysisService(factory, knownService, Options.Create(options));
    }

    [Fact]
    public async Task RunAsync_WhenLessThanTwoUnknownParticipants_ReturnsZeroAndCreatesNoGroups()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using var db = await factory.CreateDbContextAsync(ct);
        var action = InterceptionAction.Create("доповідь 100", string.Empty);
        db.InterceptionActions.Add(action);

        var message = CreateMessage(
            action,
            new DateTime(2026, 03, 25, 10, 00, 00, DateTimeKind.Utc),
            "142.4500",
            "БПЛА",
            "МИРОНОВКА");

        _ = message.AddParticipant("НВ 1", true, "оператор");
        db.InterceptionMessages.Add(message);
        await db.SaveChangesAsync(ct);

        var changed = await service.RunAsync(ct);

        changed.Should().Be(0);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        (await verifyDb.ParticipantCandidateGroups.CountAsync(ct)).Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_CreatesOpenGroupAndSynchronizesTopSuggestion()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var options = new PatternRecognitionOptions { MinConfidenceScore = 0.50 };
        var service = CreateService(factory, options);

        await using var db = await factory.CreateDbContextAsync(ct);
        var action = InterceptionAction.Create("доповідь 200", string.Empty);
        db.InterceptionActions.Add(action);

        var baseDate = new DateTime(2026, 03, 25, 12, 00, 00, DateTimeKind.Utc);

        var m1 = CreateMessage(action, baseDate, "142.4500", "БПЛА", "МИРОНОВКА");
        m1.AddLabel("МИРОНОВКА");
        m1.AddLabel("БПЛА");
        var u1 = m1.AddParticipant("НВ 1", true, "оператор", ordinal: 1);

        var m2 = CreateMessage(action, baseDate.AddMinutes(7), "142.4500", "БПЛА", "МИРОНОВКА");
        m2.AddLabel("МИРОНОВКА");
        m2.AddLabel("БПЛА");
        var u2 = m2.AddParticipant("НВ 2", true, "оператор", ordinal: 1);

        var k1 = CreateMessage(action, baseDate.AddMinutes(10), "142.4500", "БПЛА", "МИРОНОВКА");
        k1.AddLabel("МИРОНОВКА");
        k1.AddLabel("БПЛА");
        _ = k1.AddParticipant("ГРОМ", false, "оператор", ordinal: 1);

        var k2 = CreateMessage(action, baseDate.AddMinutes(18), "142.4500", "БПЛА", "МИРОНОВКА");
        k2.AddLabel("МИРОНОВКА");
        k2.AddLabel("БПЛА");
        _ = k2.AddParticipant("ГРОМ", false, "оператор", ordinal: 1);

        db.InterceptionMessages.AddRange(m1, m2, k1, k2);
        await db.SaveChangesAsync(ct);

        var changed = await service.RunAsync(ct);

        changed.Should().Be(1);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var group = await verifyDb.ParticipantCandidateGroups.SingleAsync(ct);

        group.ParticipantRefs.Should().HaveCount(2);
        group.ParticipantRefs.Select(x => x.ParticipantId).Should().BeEquivalentTo([u1.Id, u2.Id]);
        group.SuggestedName.Should().Be("ГРОМ");
        group.SuggestedRole.Should().Be("оператор");
        group.SuggestedDivision.Should().Be("БПЛА");
        group.ConfidenceScore.Should().BeGreaterThan(options.MinConfidenceScore);
    }

    [Fact]
    public async Task RunAsync_DoesNotCreateGroupFromUnknownParticipantsFromSameObservation()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        await using var db = await factory.CreateDbContextAsync(ct);
        var action = InterceptionAction.Create("доповідь 300", string.Empty);
        db.InterceptionActions.Add(action);

        var message = CreateMessage(
            action,
            new DateTime(2026, 03, 25, 14, 00, 00, DateTimeKind.Utc),
            "142.4500",
            "БПЛА",
            "МИРОНОВКА");

        message.AddLabel("МИРОНОВКА");
        _ = message.AddParticipant("НВ 1", true, "оператор", ordinal: 1);
        _ = message.AddParticipant("НВ 2", true, "оператор", ordinal: 2);

        db.InterceptionMessages.Add(message);
        await db.SaveChangesAsync(ct);

        var changed = await service.RunAsync(ct);

        changed.Should().Be(0);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        (await verifyDb.ParticipantCandidateGroups.CountAsync(ct)).Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_EnrichesExistingOpenGroupWithCompatibleUnknownFromAnotherObservation()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var options = new PatternRecognitionOptions { MinConfidenceScore = 0.50 };
        var service = CreateService(factory, options);

        Guid u1Id;
        Guid u2Id;
        Guid u3Id;

        var baseDate = new DateTime(2026, 03, 25, 16, 00, 00, DateTimeKind.Utc);

        // arrange 1: створюємо перші два НВ і known history, щоб сервіс сам сформував open-group
        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = InterceptionAction.Create("доповідь 400", string.Empty);
            db.InterceptionActions.Add(action);

            var m1 = CreateMessage(action, baseDate, "142.4500", "БПЛА", "МИРОНОВКА");
            m1.AddLabel("МИРОНОВКА");
            var u1 = m1.AddParticipant("НВ 1", true, "оператор", ordinal: 1);
            u1Id = u1.Id;

            var m2 = CreateMessage(action, baseDate.AddMinutes(5), "142.4500", "БПЛА", "МИРОНОВКА");
            m2.AddLabel("МИРОНОВКА");
            var u2 = m2.AddParticipant("НВ 2", true, "оператор", ordinal: 1);
            u2Id = u2.Id;

            var k1 = CreateMessage(action, baseDate.AddMinutes(15), "142.4500", "БПЛА", "МИРОНОВКА");
            k1.AddLabel("МИРОНОВКА");
            _ = k1.AddParticipant("ГРОМ", false, "оператор", ordinal: 1);

            db.InterceptionMessages.AddRange(m1, m2, k1);
            await db.SaveChangesAsync(ct);
        }

        var firstRunChanged = await service.RunAsync(ct);
        firstRunChanged.Should().Be(1);

        // arrange 2: додаємо третій сумісний НВ вже після створення open-group
        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = await db.InterceptionActions.SingleAsync(ct);

            var m3 = CreateMessage(action, baseDate.AddMinutes(11), "142.4500", "БПЛА", "МИРОНОВКА");
            m3.AddLabel("МИРОНОВКА");
            var u3 = m3.AddParticipant("НВ 3", true, "оператор", ordinal: 1);
            u3Id = u3.Id;

            db.InterceptionMessages.Add(m3);
            await db.SaveChangesAsync(ct);
        }

        var secondRunChanged = await service.RunAsync(ct);
        secondRunChanged.Should().Be(1);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var group = await verifyDb.ParticipantCandidateGroups.SingleAsync(ct);

        group.ParticipantRefs.Should().HaveCount(3);
        group.ParticipantRefs.Select(x => x.ParticipantId).Should().BeEquivalentTo([u1Id, u2Id, u3Id]);
        group.SuggestedDivision.Should().Be("БПЛА");
        group.SuggestedRole.Should().Be("оператор");
        group.SuggestedName.Should().Be("ГРОМ");
        group.ConfidenceScore.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task RunAsync_ForGroupWithMultipleMentions_UsesDominantDivisionAsSuggestedDivision()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var options = new PatternRecognitionOptions { MinConfidenceScore = 0.45 };
        var service = CreateService(factory, options);

        await using var db = await factory.CreateDbContextAsync(ct);
        var action = InterceptionAction.Create("доповідь 500", string.Empty);
        db.InterceptionActions.Add(action);

        var baseDate = new DateTime(2026, 03, 25, 18, 00, 00, DateTimeKind.Utc);

        var m1 = CreateMessage(action, baseDate, "142.4500", "336 мсп", "МИРОНОВКА");
        m1.AddLabel("МИРОНОВКА");
        _ = m1.AddParticipant("НВ 1", true, "оператор", ordinal: 1);

        var m2 = CreateMessage(action, baseDate.AddMinutes(6), "142.4500", "336 мсп", "МИРОНОВКА");
        m2.AddLabel("МИРОНОВКА");
        _ = m2.AddParticipant("НВ 2", true, "оператор", ordinal: 1);

        var m3 = CreateMessage(action, baseDate.AddMinutes(10), "142.4500", "186 мсп", "МИРОНОВКА");
        m3.AddLabel("МИРОНОВКА");
        _ = m3.AddParticipant("НВ 3", true, "оператор", ordinal: 1);

        var known = CreateMessage(action, baseDate.AddMinutes(14), "142.4500", "336 мсп", "МИРОНОВКА");
        known.AddLabel("МИРОНОВКА");
        _ = known.AddParticipant("ГРОМ", false, "оператор", ordinal: 1);

        db.InterceptionMessages.AddRange(m1, m2, m3, known);
        await db.SaveChangesAsync(ct);

        var changed = await service.RunAsync(ct);
        changed.Should().Be(1);

        await using var verifyDb = await factory.CreateDbContextAsync(ct);
        var group = await verifyDb.ParticipantCandidateGroups.SingleAsync(ct);

        group.ParticipantRefs.Should().HaveCount(3);
        group.SuggestedDivision.Should().Be("336 мсп");
        group.SuggestedRole.Should().Be("оператор");
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
