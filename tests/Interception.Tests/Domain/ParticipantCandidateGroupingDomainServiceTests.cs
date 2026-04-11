//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Domain;
using Interception.UI.Domain.Records;
using Interception.UI.Domain.Services;

namespace Interception.Tests.Domain;

public sealed class ParticipantCandidateGroupingDomainServiceTests
{
    [Fact]
    public void ComputeGroupFit_WhenCandidateFromSameObservation_ReturnsZero()
    {
        var options = new PatternRecognitionOptions();
        var member = CreateContext(messageId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), participantId: Guid.NewGuid(), ordinal: 1);
        var candidate = CreateContext(messageId: member.MessageId, participantId: Guid.NewGuid(), ordinal: 2);

        var fit = ParticipantCandidateGroupingDomainService.ComputeGroupFit(candidate, [member], options);

        fit.Score.Should().Be(0.0);
    }

    [Fact]
    public void RecalculateGroupScore_ForTwoCompatibleMembers_ReturnsPositiveScore()
    {
        var options = new PatternRecognitionOptions();
        var m1 = CreateContext(Guid.NewGuid(), Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), 1);
        var m2 = CreateContext(Guid.NewGuid(), Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), 1);

        var result = ParticipantCandidateGroupingDomainService.RecalculateGroupScore([m1, m2], options);

        result.Score.Should().BeGreaterThan(0.0);
    }

    private static UnknownContext CreateContext(Guid messageId, Guid participantId, int ordinal)
        => new(
            ParticipantId: participantId,
            Ordinal: ordinal,
            MessageId: messageId,
            Frequency: "145.500",
            VectorSignal: "V-01",
            PointSignal: "P-1",
            Division: "D1",
            Role: "Operator",
            ObservedDate: new DateTime(2026, 04, 11, 12, 00, 00, DateTimeKind.Utc),
            KnownPartnerNames: ["alpha", "bravo"],
            Labels: ["urgent"]);
}
