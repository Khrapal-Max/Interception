//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Analytics.Services;

namespace Interception.Tests.Application.Analytics;

/// <summary>
/// Тести для сервісу ієрархії груп.
/// Сервіс працює поверх готової карти зв'язків, тому тут достатньо фейкового ILinkMapService.
/// </summary>
public sealed class GroupHierarchyServiceTests
{
    [Fact]
    public async Task BuildAsync_NoGroups_ReturnsEmptyClusters()
    {
        var service = CreateService(new LinkMapDto([]));

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Clusters.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_CreatesHierarchy_WhenParentMemberIsCenterOfChildGroup()
    {
        var parent = CreateGroup(
            groupKey: "A",
            center: "А-1",
            members:
            [
                CreateMember("А-1", "координатор", isKeyPerson: true, connectionWeight: 3),
                CreateMember("А-2", "старший групи", connectionWeight: 4, mentionCount: 3),
                CreateMember("А-3", "оператор", connectionWeight: 2)
            ],
            primaryAction: "доповідь",
            topActions: ["доповідь", "коригування"],
            frequencies: ["402.0000"]);

        var child = CreateGroup(
            groupKey: "B",
            center: "А-2",
            members:
            [
                CreateMember("А-2", "командир", isKeyPerson: true, connectionWeight: 5),
                CreateMember("Б-1", "оператор", connectionWeight: 2),
                CreateMember("Б-2", "оператор", connectionWeight: 2)
            ],
            primaryAction: "запит",
            topActions: ["запит"],
            frequencies: ["141.4500"]);

        var service = CreateService(new LinkMapDto([parent, child]));

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Clusters.Should().HaveCount(1);

        var cluster = result.Clusters.Single();
        cluster.RootGroupKey.Should().Be("A");
        cluster.RootCenterName.Should().Be("А-1");
        cluster.TotalGroups.Should().Be(2);
        cluster.TotalUniqueMembers.Should().Be(5);
        cluster.TransitionCount.Should().Be(1);
        cluster.DirectBridgeCount.Should().Be(0);

        cluster.Nodes.Should().HaveCount(2);
        cluster.Nodes.Should().ContainSingle(x => x.GroupKey == "A" && x.IsRoot && x.Level == 0);
        cluster.Nodes.Should().ContainSingle(x =>
            x.GroupKey == "B"
            && !x.IsRoot
            && x.Level == 1
            && x.ParentGroupKey == "A"
            && x.ParentCenterName == "А-1"
            && x.TransitionMemberName == "А-2"
            && x.TransitionMemberRole == "старший групи");

        cluster.Transitions.Should().ContainSingle();
        var transition = cluster.Transitions.Single();
        transition.ParentGroupKey.Should().Be("A");
        transition.ChildGroupKey.Should().Be("B");
        transition.TransitionMemberName.Should().Be("А-2");
        transition.HasDirectBridge.Should().BeFalse();
    }

    [Fact]
    public async Task BuildAsync_PrefersBestParent_WhenChildCenterAppearsInSeveralGroups()
    {
        var strongestParent = CreateGroup(
            groupKey: "A",
            center: "А-1",
            members:
            [
                CreateMember("А-1", "координатор", isKeyPerson: true, connectionWeight: 3),
                CreateMember("Х-ЦЕНТР", "старший", connectionWeight: 9, mentionCount: 5),
                CreateMember("А-3", "оператор", connectionWeight: 2)
            ],
            bridges:
            [
                new LinkMapBridgeDto(
                    TargetGroupKey: "B",
                    TargetDivision: null,
                    ContactPersonName: "Х-ЦЕНТР",
                    BridgeFrequency: "401.2000",
                    Weight: 2,
                    PrimaryAction: "доповідь",
                    TopActions: ["доповідь"])
            ]);

        var weakerParent = CreateGroup(
            groupKey: "C",
            center: "С-1",
            members:
            [
                CreateMember("С-1", "координатор", isKeyPerson: true, connectionWeight: 3),
                CreateMember("Х-ЦЕНТР", "черговий", connectionWeight: 1, mentionCount: 1),
                CreateMember("С-3", "оператор", connectionWeight: 1)
            ]);

        var child = CreateGroup(
            groupKey: "B",
            center: "Х-ЦЕНТР",
            members:
            [
                CreateMember("Х-ЦЕНТР", "командир", isKeyPerson: true, connectionWeight: 5),
                CreateMember("Б-1", "оператор", connectionWeight: 2),
                CreateMember("Б-2", "оператор", connectionWeight: 2)
            ],
            bridges:
            [],
            internalConnectionWeight: 7,
            mentionCount: 4);

        var service = CreateService(new LinkMapDto([strongestParent, weakerParent, child]));

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Clusters.Should().HaveCount(2);

        var cluster = result.Clusters.Single(x => x.Nodes.Any(n => n.GroupKey == "B"));
        cluster.RootGroupKey.Should().Be("A");
        cluster.Nodes.Should().ContainSingle(x => x.GroupKey == "B" && x.ParentGroupKey == "A");
        cluster.Nodes.Should().NotContain(x => x.GroupKey == "B" && x.ParentGroupKey == "C");
        cluster.DirectBridgeCount.Should().Be(1);
    }

    [Fact]
    public async Task BuildAsync_RemovesCycle_ByDroppingWeakestEdge()
    {
        var groupA = CreateGroup(
            groupKey: "A",
            center: "A-1",
            members:
            [
                CreateMember("A-1", "центр", isKeyPerson: true, connectionWeight: 5),
                CreateMember("B-1", "перехід", connectionWeight: 8, mentionCount: 4),
                CreateMember("A-2", "оператор", connectionWeight: 2)
            ],
            mentionCount: 5,
            internalConnectionWeight: 10);

        var groupB = CreateGroup(
            groupKey: "B",
            center: "B-1",
            members:
            [
                CreateMember("B-1", "центр", isKeyPerson: true, connectionWeight: 5),
                CreateMember("A-1", "перехід", connectionWeight: 1, mentionCount: 1),
                CreateMember("B-2", "оператор", connectionWeight: 2)
            ],
            mentionCount: 3,
            internalConnectionWeight: 6);

        var service = CreateService(new LinkMapDto([groupA, groupB]));

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Clusters.Should().HaveCount(1);

        var cluster = result.Clusters.Single();
        cluster.TotalGroups.Should().Be(2);
        cluster.RootGroupKey.Should().Be("A");
        cluster.Nodes.Should().ContainSingle(x => x.GroupKey == "B" && x.ParentGroupKey == "A");
        cluster.Nodes.Should().NotContain(x => x.GroupKey == "A" && x.ParentGroupKey == "B");
        cluster.Transitions.Should().ContainSingle();
        cluster.Transitions.Single().TransitionMemberName.Should().Be("B-1");
    }

    [Fact]
    public async Task BuildAsync_CreatesSeparateClusters_ForUnrelatedGroups()
    {
        var groupA = CreateGroup(
            groupKey: "A",
            center: "A-1",
            members:
            [
                CreateMember("A-1", "центр", isKeyPerson: true, connectionWeight: 4),
                CreateMember("A-2", "оператор", connectionWeight: 2),
                CreateMember("A-3", "оператор", connectionWeight: 2)
            ]);

        var groupB = CreateGroup(
            groupKey: "B",
            center: "B-1",
            members:
            [
                CreateMember("B-1", "центр", isKeyPerson: true, connectionWeight: 4),
                CreateMember("B-2", "оператор", connectionWeight: 2),
                CreateMember("B-3", "оператор", connectionWeight: 2)
            ]);

        var service = CreateService(new LinkMapDto([groupA, groupB]));

        var result = await service.BuildAsync(ct: CancellationToken.None);

        result.Clusters.Should().HaveCount(2);
        result.Clusters.Should().OnlyContain(x => x.TotalGroups == 1 && x.TransitionCount == 0);
        result.Clusters.Select(x => x.RootGroupKey).Should().BeEquivalentTo(["A", "B"]);
    }

    private static GroupHierarchyService CreateService(LinkMapDto dto)
        => new(new FakeLinkMapService(dto));

    private static LinkMapGroupDto CreateGroup(
        string groupKey,
        string center,
        IReadOnlyList<LinkMapMemberDto> members,
        string? division = null,
        IReadOnlyList<string>? frequencies = null,
        string? primaryAction = "доповідь",
        IReadOnlyList<string>? topActions = null,
        IReadOnlyList<LinkMapBridgeDto>? bridges = null,
        int mentionCount = 3,
        int internalConnectionWeight = 6)
    {
        frequencies ??= ["402.0000"];
        topActions ??= primaryAction is null ? [] : [primaryAction];
        bridges ??= [];

        return new LinkMapGroupDto(
            GroupKey: groupKey,
            Division: division,
            Frequencies: frequencies,
            KeyPersonName: center,
            KeyPersonRole: members.FirstOrDefault(x => x.IsKeyPerson)?.Role,
            Members: [.. members.Select(x => x.Name)],
            MemberDetails: members,
            MentionCount: mentionCount,
            InternalConnectionWeight: internalConnectionWeight,
            BridgeWeight: bridges.Sum(x => x.Weight),
            PrimaryAction: primaryAction,
            TopActions: topActions,
            Bridges: bridges);
    }

    private static LinkMapMemberDto CreateMember(
        string name,
        string? role,
        bool isKeyPerson = false,
        int mentionCount = 2,
        int uniquePartnerCount = 2,
        int connectionWeight = 2,
        int groupCount = 1,
        bool isCrossGroup = false)
    {
        return new LinkMapMemberDto(
            Name: name,
            Role: role,
            MentionCount: mentionCount,
            UniquePartnerCount: uniquePartnerCount,
            ConnectionWeight: connectionWeight,
            LastSeenAt: new DateTime(2026, 04, 07, 9, 0, 0, DateTimeKind.Utc),
            GroupCount: groupCount,
            IsCrossGroup: isCrossGroup,
            IsKeyPerson: isKeyPerson);
    }

    private sealed class FakeLinkMapService(LinkMapDto dto) : ILinkMapService
    {
        private readonly LinkMapDto _dto = dto;

        public Task<LinkMapDto> BuildAsync(
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            CancellationToken ct = default)
            => Task.FromResult(_dto);
    }
}
