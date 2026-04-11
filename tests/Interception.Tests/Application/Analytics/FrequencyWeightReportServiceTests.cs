//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Analytics.Services;
using Interception.UI.Domain.Analytics;
using Interception.UI.Domain.Analytics.Records;
using Interception.UI.Domain.Interceptions;

namespace Interception.Tests.Application.Analytics;

public sealed class FrequencyWeightReportServiceTests
{
    [Fact]
    public async Task BuildAsync_ForOneFrequencyWithFiveUniquePersonsAcrossThreeGroups_ReturnsExpectedWeights()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();

        using (var db = factory.CreateDbContext())
        {
            var action = InterceptionAction.Create("Доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message1 = InterceptionMessage.Create(
                observedDate: new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc),
                frequency: "142.4500",
                division: "Частота дивізіон",
                vectorSignal: null,
                interceptionAction: action,
                note: null,
                createdBy: "test");

            var a1m1 = message1.AddParticipant("АЛЬФА-1", isUnknown: false);
            var a2m1 = message1.AddParticipant("АЛЬФА-2", isUnknown: false);
            var b1m1 = message1.AddParticipant("БРАВО-1", isUnknown: false);
            _ = message1.AddParticipant("НВ 1", isUnknown: true);

            var message2 = InterceptionMessage.Create(
                observedDate: new DateTime(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc),
                frequency: "142.4500",
                division: "Частота дивізіон",
                vectorSignal: null,
                interceptionAction: action,
                note: null,
                createdBy: "test");

            var a1m2 = message2.AddParticipant("АЛЬФА-1", isUnknown: false);
            var a2m2 = message2.AddParticipant("АЛЬФА-2", isUnknown: false);
            var b1m2 = message2.AddParticipant("БРАВО-1", isUnknown: false);
            var b2m2 = message2.AddParticipant("БРАВО-2", isUnknown: false);
            _ = message2.AddParticipant("НВ 1", isUnknown: true);

            var message3 = InterceptionMessage.Create(
                observedDate: new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc),
                frequency: "142.4500",
                division: "Частота дивізіон",
                vectorSignal: null,
                interceptionAction: action,
                note: null,
                createdBy: "test");

            var b2m3 = message3.AddParticipant("БРАВО-2", isUnknown: false);

            db.InterceptionMessages.AddRange(message1, message2, message3);

            var resolvedA1 = ResolvedParticipant.Create("АЛЬФА-1", "test", division: "Підрозділ А");
            var resolvedA2 = ResolvedParticipant.Create("АЛЬФА-2", "test", division: "Підрозділ А");
            var resolvedB1 = ResolvedParticipant.Create("БРАВО-1", "test", division: "Підрозділ Б");
            var resolvedB2 = ResolvedParticipant.Create("БРАВО-2", "test", division: "Підрозділ Б");

            db.ResolvedParticipants.AddRange(resolvedA1, resolvedA2, resolvedB1, resolvedB2);

            db.ParticipantCandidateGroups.AddRange(
                CreateConfirmedGroup(resolvedA1.Id, a1m1, a1m2),
                CreateConfirmedGroup(resolvedA2.Id, a2m1, a2m2),
                CreateConfirmedGroup(resolvedB1.Id, b1m1, b1m2),
                CreateConfirmedGroup(resolvedB2.Id, b2m2, b2m3));

            db.SaveChanges();
        }

        var service = new FrequencyWeightReportService(factory);

        var report = await service.BuildAsync(ct);
        var frequency = Assert.Single(report.Frequencies);

        Assert.Equal("142.4500", frequency.Frequency);
        Assert.Equal(5, frequency.PersonsCount);
        Assert.Equal(3, frequency.Groups.Count);

        AssertGroup(frequency, "Підрозділ А", expectedPersonsCount: 2, expectedWeightPercent: 40m);
        AssertGroup(frequency, "Підрозділ Б", expectedPersonsCount: 2, expectedWeightPercent: 40m);
        AssertGroup(frequency, "Частота дивізіон", expectedPersonsCount: 1, expectedWeightPercent: 20m);
    }

    [Fact]
    public async Task BuildAsync_WhenSameResolvedPersonAppearsInThreeMessages_CountsThatPersonOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();

        using (var db = factory.CreateDbContext())
        {
            var action = InterceptionAction.Create("Доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message1 = CreateMessage(action, "150.0000", "АЛЬФА", new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc));
            var p1 = message1.AddParticipant("ШАПКА", isUnknown: false);
            _ = message1.AddParticipant("НВ 1", isUnknown: true);

            var message2 = CreateMessage(action, "150.0000", "АЛЬФА", new DateTime(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc));
            var p2 = message2.AddParticipant("ШАПКА", isUnknown: false);

            var message3 = CreateMessage(action, "150.0000", "АЛЬФА", new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc));
            var p3 = message3.AddParticipant("ШАПКА", isUnknown: false);

            db.InterceptionMessages.AddRange(message1, message2, message3);

            var resolved = ResolvedParticipant.Create("ШАПКА", "test", division: "Підрозділ А");
            db.ResolvedParticipants.Add(resolved);
            db.ParticipantCandidateGroups.Add(CreateConfirmedGroup(resolved.Id, p1, p2, p3));

            db.SaveChanges();
        }

        var service = new FrequencyWeightReportService(factory);

        var report = await service.BuildAsync(ct);
        var frequency = Assert.Single(report.Frequencies);

        Assert.Equal(2, frequency.PersonsCount);
        AssertGroup(frequency, "Підрозділ А", expectedPersonsCount: 1, expectedWeightPercent: 50m);
        AssertGroup(frequency, "АЛЬФА", expectedPersonsCount: 1, expectedWeightPercent: 50m);
    }

    [Fact]
    public async Task BuildAsync_WhenConfirmedAndUnresolvedRowsRepresentSameKnownPerson_CountsPersonOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();

        using (var db = factory.CreateDbContext())
        {
            var action = InterceptionAction.Create("Доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message1 = CreateMessage(action, "160.0000", "АЛЬФА", new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc));
            var p1 = message1.AddParticipant("КЛИМ", isUnknown: false);

            var message2 = CreateMessage(action, "160.0000", "АЛЬФА", new DateTime(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc));
            var p2 = message2.AddParticipant("КЛИМ", isUnknown: false);

            var message3 = CreateMessage(action, "160.0000", "АЛЬФА", new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc));
            _ = message3.AddParticipant("КЛИМ", isUnknown: false);
            _ = message3.AddParticipant("НВ 7", isUnknown: true);

            db.InterceptionMessages.AddRange(message1, message2, message3);

            var resolved = ResolvedParticipant.Create("КЛИМ", "test", division: "Підрозділ А");
            db.ResolvedParticipants.Add(resolved);
            db.ParticipantCandidateGroups.Add(CreateConfirmedGroup(resolved.Id, p1, p2));

            db.SaveChanges();
        }

        var service = new FrequencyWeightReportService(factory);

        var report = await service.BuildAsync(ct);
        var frequency = Assert.Single(report.Frequencies);

        // КЛИМ рахується один раз, а unknown бере fallback по division зі сповіщення.
        Assert.Equal(2, frequency.PersonsCount);
        AssertGroup(frequency, "Підрозділ А", expectedPersonsCount: 1, expectedWeightPercent: 50m);
        AssertGroup(frequency, "АЛЬФА", expectedPersonsCount: 1, expectedWeightPercent: 50m);
    }

    [Fact]
    public async Task BuildAsync_PrefersCanonicalIdentityOverOpenGroupIdentity_ForKnownAlias()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();

        using (var db = factory.CreateDbContext())
        {
            var action = InterceptionAction.Create("Доповідь", string.Empty);
            db.InterceptionActions.Add(action);

            var message1 = CreateMessage(action, "170.0000", "АЛЬФА", new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc));
            var aliasParticipant = message1.AddParticipant("ШАПКА-2", isUnknown: false);

            var message2 = CreateMessage(action, "170.0000", "АЛЬФА", new DateTime(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc));
            var confirmedParticipant = message2.AddParticipant("ШАПКА-1", isUnknown: false);

            var message3 = CreateMessage(action, "170.0000", "АЛЬФА", new DateTime(2026, 4, 2, 9, 30, 0, DateTimeKind.Utc));
            var aliasParticipantFollowUp = message3.AddParticipant("ШАПКА-2", isUnknown: false);

            var message4 = CreateMessage(action, "170.0000", "АЛЬФА", new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc));
            var confirmedParticipantFollowUp = message4.AddParticipant("ШАПКА-1", isUnknown: false);

            db.InterceptionMessages.AddRange(message1, message2, message3, message4);

            var resolvedA = ResolvedParticipant.Create("ШАПКА-1", "test", division: "Підрозділ А");
            var resolvedB = ResolvedParticipant.Create("ШАПКА-2", "test", division: "Підрозділ А");
            db.ResolvedParticipants.AddRange(resolvedA, resolvedB);

            var canonical = CanonicalPerson.Create("ШАПКА");
            canonical.AddMember(resolvedA.Id);
            canonical.AddMember(resolvedB.Id);
            db.CanonicalPersons.Add(canonical);

            db.ParticipantCandidateGroups.Add(CreateConfirmedGroup(resolvedA.Id, confirmedParticipant, confirmedParticipantFollowUp));

            var openGroup = ParticipantCandidateGroup.Create(
                [
                    new ParticipantRef(aliasParticipant.InterceptionMessageId, aliasParticipant.Id, aliasParticipant.Ordinal),
                    new ParticipantRef(aliasParticipantFollowUp.InterceptionMessageId, aliasParticipantFollowUp.Id, aliasParticipantFollowUp.Ordinal)
                ],
                confidenceScore: 0.70,
                reasons: new PatternMatchReasons { SameFrequency = true },
                suggestedName: aliasParticipant.Name);
            db.ParticipantCandidateGroups.Add(openGroup);

            db.SaveChanges();
        }

        var service = new FrequencyWeightReportService(factory);
        var report = await service.BuildAsync(ct);
        var frequency = Assert.Single(report.Frequencies);

        Assert.Equal(1, frequency.PersonsCount);
        AssertGroup(frequency, "Підрозділ А", expectedPersonsCount: 1, expectedWeightPercent: 100m);
    }

    private static InterceptionMessage CreateMessage(
        InterceptionAction action,
        string frequency,
        string? division,
        DateTime observedDate)
    {
        return InterceptionMessage.Create(
            observedDate: observedDate,
            frequency: frequency,
            division: division,
            vectorSignal: null,
            interceptionAction: action,
            note: null,
            createdBy: "test");
    }

    private static ParticipantCandidateGroup CreateConfirmedGroup(Guid resolvedParticipantId, params InterceptionMessageParticipant[] participants)
    {
        var refs = participants
            .Select(x => new ParticipantRef(x.InterceptionMessageId, x.Id, x.Ordinal))
            .ToList();

        var group = ParticipantCandidateGroup.Create(
            refs,
            confidenceScore: 0.95,
            reasons: new PatternMatchReasons
            {
                SameFrequency = true,
                SameDivision = true,
                CloseInTime = true
            },
            suggestedName: participants.First().Name);

        group.Confirm(participants.First().Name ?? "resolved", "test", resolvedParticipantId);
        return group;
    }

    private static void AssertGroup(
        FrequencyWeightDto frequency,
        string groupName,
        int expectedPersonsCount,
        decimal expectedWeightPercent)
    {
        var group = Assert.Single(frequency.Groups, x => x.GroupName == groupName);
        Assert.Equal(expectedPersonsCount, group.PersonsCount);
        Assert.Equal(expectedWeightPercent, group.WeightPercent);
    }
}
