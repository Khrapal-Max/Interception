//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.Application.Analytics.Services;
using Interception.Domain.Entities;
using Interception.Domain.Records;
using Interception.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Interception.Tests.Application.Analytics;

public sealed class ContextSuggestionServiceTests
{
    [Fact]
    public async Task GetContextSuggestionsAsync_GroupNotFound_ReturnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = CreateService(factory);

        var result = await service.GetContextSuggestionsAsync(Guid.NewGuid(), 3, ct);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetContextSuggestionsAsync_MatchingKnownAndConfirmedContext_ReturnsRankedSuggestion()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();

        Guid openGroupId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = SeedAction(db);
            var observed = new DateTime(2026, 3, 25, 10, 0, 0, DateTimeKind.Utc);

            SeedKnownMessage(
                db,
                action,
                observed.AddMinutes(-30),
                name: "ШАПКА",
                role: "оператор",
                frequency: "157.0250",
                division: "1 БПЛА",
                vector: "МИРОНОВКА",
                labels: ["МИРОНОВКА", "БПЛА"]);

            SeedKnownMessage(
                db,
                action,
                observed.AddMinutes(-25),
                name: "ВОЛГА",
                role: "оператор",
                frequency: "157.0250",
                division: "1 БПЛА",
                vector: "МИРОНОВКА",
                labels: ["МИРОНОВКА"]);

            var (Message, Participant) = SeedUnknownMessage(
                db,
                action,
                observed,
                role: "оператор",
                frequency: "157.0250",
                division: null,
                vector: "МИРОНОВКА",
                labels: ["МИРОНОВКА", "БПЛА"]);

            var openUnknown2 = SeedUnknownMessage(
                db,
                action,
                observed.AddMinutes(2),
                role: "оператор",
                frequency: "157.0250",
                division: null,
                vector: "МИРОНОВКА",
                labels: ["МИРОНОВКА"]);

            var openGroup = ParticipantCandidateGroup.Create(
                [
                    new ParticipantRef(Message.Id, Participant.Id, Participant.Ordinal),
                    new ParticipantRef(openUnknown2.Message.Id, openUnknown2.Participant.Id, openUnknown2.Participant.Ordinal)
                ],
                confidenceScore: 0.81,
                reasons: new PatternMatchReasons { SameFrequency = true, SameVector = true, SharedLabels = true },
                suggestedName: null,
                suggestedRole: null,
                suggestedDivision: null);

            var confirmedUnknown1 = SeedUnknownMessage(
                db,
                action,
                observed.AddHours(-3),
                role: "оператор",
                frequency: "157.0250",
                division: null,
                vector: "МИРОНОВКА",
                labels: ["МИРОНОВКА", "БПЛА"]);

            var confirmedUnknown2 = SeedUnknownMessage(
                db,
                action,
                observed.AddHours(-2).AddMinutes(-55),
                role: "оператор",
                frequency: "157.0250",
                division: null,
                vector: "МИРОНОВКА",
                labels: ["БПЛА"]);

            var confirmedGroup = ParticipantCandidateGroup.Create(
                [
                    new ParticipantRef(confirmedUnknown1.Message.Id, confirmedUnknown1.Participant.Id, confirmedUnknown1.Participant.Ordinal),
                    new ParticipantRef(confirmedUnknown2.Message.Id, confirmedUnknown2.Participant.Id, confirmedUnknown2.Participant.Ordinal)
                ],
                confidenceScore: 0.92,
                reasons: new PatternMatchReasons { SameFrequency = true, SameVector = true, SharedLabels = true },
                suggestedName: null,
                suggestedRole: "оператор",
                suggestedDivision: "1 БПЛА");

            confirmedGroup.Confirm("ГРОМ", "tester");

            db.ParticipantCandidateGroups.AddRange(openGroup, confirmedGroup);
            await db.SaveChangesAsync(ct);

            openGroupId = openGroup.Id;
        }

        var service = CreateService(factory);
        var result = await service.GetContextSuggestionsAsync(openGroupId, 3, ct);

        result.Should().NotBeEmpty();

        var suggestion = result[0];
        suggestion.Division.Should().Be("1 БПЛА");
        suggestion.SuggestedRole.Should().Be("оператор");
        suggestion.MatchScore.Should().BeGreaterThan(0);
        suggestion.SeenCount.Should().BeGreaterThanOrEqualTo(4);
        suggestion.ConfirmedGroupCount.Should().Be(1);
        suggestion.RelatedKnownNames.Should().Contain(["ШАПКА", "ВОЛГА"]);
        suggestion.RelatedResolvedNames.Should().Contain("ГРОМ");
        suggestion.CommonLabels.Should().Contain("МИРОНОВКА");
        suggestion.Reasons.SameFrequency.Should().BeTrue();
        suggestion.Reasons.SameVector.Should().BeTrue();
        suggestion.Reasons.SameRole.Should().BeTrue();
        suggestion.Reasons.SharedLabels.Should().BeTrue();
        suggestion.Reasons.HasConfirmedContext.Should().BeTrue();
    }

    [Fact]
    public async Task GetContextSuggestionsAsync_ConfirmedContextWithoutFeatureOverlap_IsIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();

        Guid openGroupId;

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var action = SeedAction(db);
            var observed = new DateTime(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc);

            var openUnknown1 = SeedUnknownMessage(
                db,
                action,
                observed,
                role: "оператор",
                frequency: "157.0250",
                division: null,
                vector: "МИРОНОВКА",
                labels: ["МИРОНОВКА"]);

            var openUnknown2 = SeedUnknownMessage(
                db,
                action,
                observed.AddMinutes(1),
                role: "оператор",
                frequency: "157.0250",
                division: null,
                vector: "МИРОНОВКА",
                labels: ["МИРОНОВКА"]);

            var openGroup = ParticipantCandidateGroup.Create(
                [
                    new ParticipantRef(openUnknown1.Message.Id, openUnknown1.Participant.Id, openUnknown1.Participant.Ordinal),
                    new ParticipantRef(openUnknown2.Message.Id, openUnknown2.Participant.Id, openUnknown2.Participant.Ordinal)
                ],
                confidenceScore: 0.70,
                reasons: new PatternMatchReasons { SameFrequency = true },
                suggestedName: null,
                suggestedRole: null,
                suggestedDivision: null);

            var confirmedUnknown1 = SeedUnknownMessage(
                db,
                action,
                observed.AddHours(-5),
                role: "водій",
                frequency: "410.1370",
                division: null,
                vector: "ІНШИЙ ВЕКТОР",
                labels: ["ІНША МІТКА"]);

            var (Message, Participant) = SeedUnknownMessage(
                db,
                action,
                observed.AddHours(-4).AddMinutes(10),
                role: "водій",
                frequency: "410.1370",
                division: null,
                vector: "ІНШИЙ ВЕКТОР",
                labels: ["ІНША МІТКА"]);

            var confirmedGroup = ParticipantCandidateGroup.Create(
                [
                    new ParticipantRef(confirmedUnknown1.Message.Id, confirmedUnknown1.Participant.Id, confirmedUnknown1.Participant.Ordinal),
                    new ParticipantRef(Message.Id, Participant.Id, Participant.Ordinal)
                ],
                confidenceScore: 0.90,
                reasons: new PatternMatchReasons { SameFrequency = true },
                suggestedName: null,
                suggestedRole: "водій",
                suggestedDivision: "2 МСБ");

            confirmedGroup.Confirm("БУРАН", "tester");

            db.ParticipantCandidateGroups.AddRange(openGroup, confirmedGroup);
            await db.SaveChangesAsync(ct);

            openGroupId = openGroup.Id;
        }

        var service = CreateService(factory);
        var result = await service.GetContextSuggestionsAsync(openGroupId, 3, ct);

        result.Should().BeEmpty();
    }

    private static ContextSuggestionService CreateService(IDbContextFactory<PostgreSqlDbContext> factory)
        => new(factory, Options.Create(new PatternRecognitionOptions()));

    private static InterceptionAction SeedAction(PostgreSqlDbContext db)
    {
        var action = InterceptionAction.Create("координація дій", string.Empty);
        db.InterceptionActions.Add(action);
        return action;
    }

    private static (InterceptionMessage Message, InterceptionMessageParticipant Participant) SeedKnownMessage(
        PostgreSqlDbContext db,
        InterceptionAction action,
        DateTime observedDate,
        string name,
        string? role,
        string? frequency,
        string? division,
        string? vector,
        IReadOnlyList<string> labels)
    {
        var message = InterceptionMessage.Create(
            observedDate,
            frequency,
            division,
            vector,
            action,
            note: null,
            createdBy: "tester");

        var participant = message.AddParticipant(name, isUnknown: false, role, ordinal: 1);
        foreach (var label in labels)
            message.AddLabel(label);

        db.InterceptionMessages.Add(message);
        return (message, participant);
    }

    private static (InterceptionMessage Message, InterceptionMessageParticipant Participant) SeedUnknownMessage(
        PostgreSqlDbContext db,
        InterceptionAction action,
        DateTime observedDate,
        string? role,
        string? frequency,
        string? division,
        string? vector,
        IReadOnlyList<string> labels)
    {
        var message = InterceptionMessage.Create(
            observedDate,
            frequency,
            division,
            vector,
            action,
            note: null,
            createdBy: "tester");

        var participant = message.AddParticipant("НВ", isUnknown: true, role, ordinal: 1);
        foreach (var label in labels)
            message.AddLabel(label);

        db.InterceptionMessages.Add(message);
        return (message, participant);
    }
}
