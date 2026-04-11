//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Analytics.Services;
using Interception.UI.Domain.Interceptions;
using Interception.UI.Domain.Interceptions.Enums;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Interception.Tests.Application.Analytics;

/// <summary>
/// Тести для перевірки інтеграції explicit контуру керування в ієрархію груп.
/// </summary>
public sealed class GroupHierarchyServiceTests
{
    [Fact]
    public async Task BuildAsync_WhenExplicitDirectiveExists_BuildsDirectiveParentChildEdge()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var linkMapService = Substitute.For<ILinkMapService>();

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var fromResolved = ResolvedParticipant.Create("А-ЦЕНТР", "analyst", division: "Підрозділ А");
            var toResolved = ResolvedParticipant.Create("Б-ЦЕНТР", "analyst", division: "Підрозділ Б");

            db.ResolvedParticipants.AddRange(fromResolved, toResolved);

            db.PersonDirectiveRelations.Add(PersonDirectiveRelation.Create(
                fromCanonicalPersonId: null,
                fromResolvedParticipantId: fromResolved.Id,
                toCanonicalPersonId: null,
                toResolvedParticipantId: toResolved.Id,
                relationType: DirectiveRelationType.Command,
                confidence: DirectiveRelationConfidence.High,
                sourceObservationId: null,
                isManual: true,
                comment: "Явний наказ"));

            await db.SaveChangesAsync(ct);
        }

        linkMapService.BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new LinkMapDto([
                CreateGroup("GROUP-A", "Підрозділ А", "А-ЦЕНТР", []),
                CreateGroup("GROUP-B", "Підрозділ Б", "Б-ЦЕНТР", [])
            ]));

        var service = new GroupHierarchyService(linkMapService, factory);

        var map = await service.BuildAsync(ct: ct);

        map.RootGroupCount.Should().Be(1);
        map.ChildGroupCount.Should().Be(1);
        map.NeedsReviewCount.Should().Be(0);

        var cluster = map.Clusters.Should().ContainSingle().Subject;
        cluster.Root.GroupKey.Should().Be("GROUP-A");
        cluster.Root.Title.Should().Be("Підрозділ А");

        var edge = cluster.Edges.Should().ContainSingle().Subject;
        edge.ParentGroupKey.Should().Be("GROUP-A");
        edge.ChildGroupKey.Should().Be("GROUP-B");
        edge.IsDirective.Should().BeTrue();
        edge.ViaMemberName.Should().Be("А-ЦЕНТР");
        edge.DirectiveLabel.Should().Contain("явний наказ");
        edge.DirectiveLabel.Should().Contain("висока впевненість");
    }

    [Fact]
    public async Task BuildAsync_WhenDirectiveConflictsWithReverseGraphEdge_DirectiveWinsAndClusterNeedsReview()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = TestDbFactory.CreateFactory();
        var linkMapService = Substitute.For<ILinkMapService>();

        await using (var db = await factory.CreateDbContextAsync(ct))
        {
            var fromResolved = ResolvedParticipant.Create("А-ЦЕНТР", "analyst", division: "Підрозділ А");
            var toResolved = ResolvedParticipant.Create("Б-ЦЕНТР", "analyst", division: "Підрозділ Б");

            db.ResolvedParticipants.AddRange(fromResolved, toResolved);

            db.PersonDirectiveRelations.Add(PersonDirectiveRelation.Create(
                fromCanonicalPersonId: null,
                fromResolvedParticipantId: fromResolved.Id,
                toCanonicalPersonId: null,
                toResolvedParticipantId: toResolved.Id,
                relationType: DirectiveRelationType.Control,
                confidence: DirectiveRelationConfidence.Medium,
                sourceObservationId: null,
                isManual: true,
                comment: "А контролює Б"));

            await db.SaveChangesAsync(ct);
        }

        // Базова graph-евристика штовхає у протилежний бік:
        // у складі групи Б є member "А-ЦЕНТР", тобто без explicit relation сервіс спробує побудувати B -> A.
        linkMapService.BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new LinkMapDto([
                CreateGroup("GROUP-A", "Підрозділ А", "А-ЦЕНТР", []),
                CreateGroup("GROUP-B", "Підрозділ Б", "Б-ЦЕНТР",
                [
                    new LinkMapMemberDto("А-ЦЕНТР", "координатор", 1, 1, 1, Now, 1, false, false)
                ])
            ]));

        var service = new GroupHierarchyService(linkMapService, factory);

        var map = await service.BuildAsync(ct: ct);

        map.ChildGroupCount.Should().Be(1);
        map.NeedsReviewCount.Should().BeGreaterThan(0);

        var cluster = map.Clusters.Should().ContainSingle().Subject;
        cluster.Root.GroupKey.Should().Be("GROUP-A", "explicit relation A -> B має перемогти слабке reverse graph-ребро");
        cluster.NeedsReview.Should().BeTrue("конфлікт між graph та explicit relation має переводити кейс у review");

        var edge = cluster.Edges.Should().ContainSingle().Subject;
        edge.ParentGroupKey.Should().Be("GROUP-A");
        edge.ChildGroupKey.Should().Be("GROUP-B");
        edge.IsDirective.Should().BeTrue();
        edge.DirectiveLabel.Should().Contain("явний контроль");
        edge.DirectiveLabel.Should().Contain("середня впевненість");
    }

    private static readonly DateTime Now = new(2026, 04, 09, 12, 00, 00, DateTimeKind.Utc);

    private static LinkMapGroupDto CreateGroup(
        string groupKey,
        string division,
        string keyPersonName,
        IReadOnlyList<LinkMapMemberDto> extraMembers)
    {
        var members = new List<LinkMapMemberDto>
        {
            new(keyPersonName, "координатор", 3, 2, 4, Now, 1, false, true),
            new($"{keyPersonName}-1", "оператор", 1, 1, 1, Now, 1, false, false)
        };

        members.AddRange(extraMembers);

        return new LinkMapGroupDto(
             GroupKey: groupKey,
             Division: division,
             Frequencies: ["402.0000"],
             KeyPersonName: keyPersonName,
             KeyPersonRole: "координатор",
             Members: [.. members.Select(x => x.Name)],
             MemberDetails: members,
             MentionCount: members.Sum(x => x.MentionCount),
             InternalConnectionWeight: members.Sum(x => x.ConnectionWeight),
             BridgeWeight: 0,
             PrimaryAction: "координація",
             TopActions: ["координація"],
             Bridges: []);
    }

    private sealed class TestDbFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        private TestDbFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public static IDbContextFactory<AppDbContext> CreateFactory()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"group-hierarchy-tests-{Guid.NewGuid()}")
                .EnableSensitiveDataLogging()
                .Options;

            return new TestDbFactory(options);
        }

        public AppDbContext CreateDbContext()
            => new(_options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
