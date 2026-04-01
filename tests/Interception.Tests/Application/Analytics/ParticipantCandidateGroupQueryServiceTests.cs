//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Analytics.Services;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Interception.UI.Domain.Records;

namespace Interception.Tests.Application.Analytics;

public sealed class ParticipantCandidateGroupQueryServiceTests
{
    private static ParticipantCandidateGroupQueryService CreateService()
        => new(TestDbFactory.CreateFactory());

    [Fact]
    public async Task GetGroupsByStatusAsync_WhenNoGroups_ReturnsEmptyPagedResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService();

        var result = await service.GetGroupsByStatusAsync(CandidateGroupStatus.Open, 1, 20, ct);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.TotalPages.Should().Be(0);
        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetGroupsByStatusAsync_ReturnsOnlyRequestedStatus_OrderedByConfidence_AndPaged()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = new ParticipantCandidateGroupQueryService(factory);

        await using var db = await factory.CreateDbContextAsync(ct);
        var action = InterceptionAction.Create("доповідь 200", string.Empty);
        db.InterceptionActions.Add(action);

        var m1 = CreateMessage(action, new DateTime(2026, 03, 25, 10, 00, 00, DateTimeKind.Utc), "142.4500", "БПЛА", "МИРОНОВКА");
        var p11 = m1.AddParticipant("НВ 1", true, "оператор");
        var p12 = m1.AddParticipant("НВ 2", true, "оператор");

        var m2 = CreateMessage(action, new DateTime(2026, 03, 25, 10, 10, 00, DateTimeKind.Utc), "143.4500", "БПЛА", "МИРОНОВКА");
        var p21 = m2.AddParticipant("НВ 3", true, "оператор");
        var p22 = m2.AddParticipant("НВ 4", true, "оператор");

        var m3 = CreateMessage(action, new DateTime(2026, 03, 25, 10, 20, 00, DateTimeKind.Utc), "144.4500", "ШТУРМ", "СХІД");
        var p31 = m3.AddParticipant("НВ 5", true, "спостерігач");
        var p32 = m3.AddParticipant("НВ 6", true, "спостерігач");

        db.InterceptionMessages.AddRange(m1, m2, m3);
        await db.SaveChangesAsync(ct);

        var high = ParticipantCandidateGroup.Create(
            [new ParticipantRef(p11.InterceptionMessageId, p11.Id, p11.Ordinal), new ParticipantRef(p12.InterceptionMessageId, p12.Id, p12.Ordinal)],
            0.92,
            new PatternMatchReasons { SameFrequency = true, SameVector = true });

        var medium = ParticipantCandidateGroup.Create(
            [new ParticipantRef(p21.InterceptionMessageId, p21.Id, p21.Ordinal), new ParticipantRef(p22.InterceptionMessageId, p22.Id, p22.Ordinal)],
            0.61,
            new PatternMatchReasons { SameFrequency = true });

        var lowDismissed = ParticipantCandidateGroup.Create(
            [new ParticipantRef(p31.InterceptionMessageId, p31.Id, p31.Ordinal), new ParticipantRef(p32.InterceptionMessageId, p32.Id, p32.Ordinal)],
            0.20,
            new PatternMatchReasons { CloseInTime = true });
        lowDismissed.Dismiss("tester");

        db.ParticipantCandidateGroups.AddRange(high, medium, lowDismissed);
        await db.SaveChangesAsync(ct);

        var result = await service.GetGroupsByStatusAsync(CandidateGroupStatus.Open, page: 1, pageSize: 1, ct);

        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(1);
        result.TotalPages.Should().Be(2);
        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(high.Id);
        result.Items[0].ConfidenceScore.Should().Be(0.92);
        result.Items[0].Status.Should().Be(CandidateGroupStatus.Open);
    }

    [Fact]
    public async Task GetGroupByIdAsync_ReturnsNull_WhenGroupDoesNotExist()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService();

        var result = await service.GetGroupByIdAsync(Guid.NewGuid(), ct);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGroupByIdAsync_ReturnsEnrichedDto_WithMessageSnapshotsAndResolutionData()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var service = new ParticipantCandidateGroupQueryService(factory);

        await using var db = await factory.CreateDbContextAsync(ct);
        var action = InterceptionAction.Create("доповідь 300", string.Empty);
        db.InterceptionActions.Add(action);

        var observedDate = new DateTime(2026, 03, 25, 12, 30, 00, DateTimeKind.Utc);
        var message = CreateMessage(action, observedDate, "142.4500", "БПЛА", "МИРОНОВКА");
        var p1 = message.AddParticipant("НВ 7", true, "оператор", ordinal: 1);
        var p2 = message.AddParticipant("НВ 8", true, "оператор", ordinal: 2);

        db.InterceptionMessages.Add(message);
        await db.SaveChangesAsync(ct);

        var resolvedParticipantId = Guid.NewGuid();
        var group = ParticipantCandidateGroup.Create(
            [new ParticipantRef(p1.InterceptionMessageId, p1.Id, p1.Ordinal), new ParticipantRef(p2.InterceptionMessageId, p2.Id, p2.Ordinal)],
            0.87,
            new PatternMatchReasons
            {
                SameFrequency = true,
                SameVector = true,
                SameDivision = true,
                SharedLabels = true,
                SharedPartners = true,
            },
            suggestedName: "ГРОМ",
            suggestedRole: "оператор",
            suggestedDivision: "БПЛА");

        group.Confirm("ГРОМ", "tester", resolvedParticipantId);

        db.ParticipantCandidateGroups.Add(group);
        await db.SaveChangesAsync(ct);

        var dto = await service.GetGroupByIdAsync(group.Id, ct);

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(group.Id);
        dto.Status.Should().Be(CandidateGroupStatus.Confirmed);
        dto.ConfidenceScore.Should().Be(0.87);
        dto.SuggestedName.Should().Be("ГРОМ");
        dto.SuggestedRole.Should().Be("оператор");
        dto.SuggestedDivision.Should().Be("БПЛА");
        dto.ResolvedParticipantId.Should().Be(resolvedParticipantId);
        dto.ResolvedBy.Should().Be("tester");
        dto.ResolvedAt.Should().NotBeNull();
        dto.CreatedAt.Should().NotBe(default);

        dto.Reasons.SameFrequency.Should().BeTrue();
        dto.Reasons.SameVector.Should().BeTrue();
        dto.Reasons.SameDivision.Should().BeTrue();
        dto.Reasons.SharedLabels.Should().BeTrue();
        dto.Reasons.SharedPartners.Should().BeTrue();

        dto.Refs.Should().HaveCount(2);
        dto.Refs.Should().OnlyContain(r => r.MessageId == message.Id);
        dto.Refs.Should().OnlyContain(r => r.ObservedDate == observedDate);
        dto.Refs.Should().OnlyContain(r => r.Frequency == "142.4500");
        dto.Refs.Should().OnlyContain(r => r.VectorSignal == "МИРОНОВКА");
        dto.Refs.Should().OnlyContain(r => r.Division == "БПЛА");
        dto.Refs.Select(r => r.Ordinal).Should().BeEquivalentTo([1, 2]);
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
