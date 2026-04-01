//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Analytics.Services;

namespace Interception.Tests.Application.Analytics;

public sealed class LinkMapSliceServiceTests
{
    [Fact]
    public async Task BuildAsync_ProjectsGroupCompletenessAndSortsMissingFirst()
    {
        var map = new LinkMapDto([
            new LinkMapGroupDto(
                GroupKey: "g1",
                Division: null,
                Frequencies: ["401.100"],
                KeyPersonName: "ЦЕНТР-1",
                KeyPersonRole: "координатор",
                Members: ["ЦЕНТР-1", "А"],
                MemberDetails:
                [
                    new LinkMapMemberDto("ЦЕНТР-1", "координатор", 3, 2, 4, new DateTime(2026,3,30), 1, false, true),
                    new LinkMapMemberDto("А", null, 2, 1, 2, new DateTime(2026,3,30), 1, false, false)
                ],
                MentionCount: 3,
                InternalConnectionWeight: 4,
                BridgeWeight: 2,
                PrimaryAction: "доповідь",
                TopActions: ["доповідь", "команда"],
                Bridges: []),
            new LinkMapGroupDto(
                GroupKey: "g2",
                Division: "336 мсп",
                Frequencies: ["145.000"],
                KeyPersonName: "ЦЕНТР-2",
                KeyPersonRole: "координатор",
                Members: ["ЦЕНТР-2", "Б"],
                MemberDetails:
                [
                    new LinkMapMemberDto("ЦЕНТР-2", "координатор", 3, 2, 4, new DateTime(2026,3,30), 1, false, true),
                    new LinkMapMemberDto("Б", "оператор", 2, 1, 2, new DateTime(2026,3,30), 1, false, false)
                ],
                MentionCount: 3,
                InternalConnectionWeight: 4,
                BridgeWeight: 2,
                PrimaryAction: "команда",
                TopActions: ["команда"],
                Bridges: [])
        ]);

        var service = new LinkMapSliceService(new StubLinkMapService(map));

        var result = await service.BuildAsync();

        result.Groups.Should().HaveCount(2);
        result.Groups[0].GroupKey.Should().Be("g1");
        result.Groups[0].IsDivisionMissing.Should().BeTrue();
        result.Groups[0].MissingRoleCount.Should().Be(1);
        result.Groups[0].SharedActions.Should().BeEquivalentTo(["доповідь", "команда"]);

        result.Groups[1].GroupKey.Should().Be("g2");
        result.Groups[1].IsDivisionMissing.Should().BeFalse();
        result.Groups[1].MissingRoleCount.Should().Be(0);
    }

    private sealed class StubLinkMapService(LinkMapDto map) : ILinkMapService
    {
        public Task<LinkMapDto> BuildAsync(DateTime? dateFrom = null, DateTime? dateTo = null, CancellationToken ct = default)
            => Task.FromResult(map);
    }
}
