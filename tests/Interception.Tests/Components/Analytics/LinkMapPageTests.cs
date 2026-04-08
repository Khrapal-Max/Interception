//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Exports.Abstractions;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Analytics.LinkMap;
using Interception.UI.Components.Pages.Analytics.LinkMap.Drawers;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Interception.Tests.Components.Analytics;

public sealed class LinkMapPageTests : BunitContext
{
    private readonly ILinkMapService _linkMapService;
    private readonly ITopologySnapshotBuilder _topologySnapshotBuilder;
    private readonly IExcelExportService _excelExportService;

    public LinkMapPageTests()
    {
        _linkMapService = Substitute.For<ILinkMapService>();
        _topologySnapshotBuilder = Substitute.For<ITopologySnapshotBuilder>();
        _excelExportService = Substitute.For<IExcelExportService>();

        Services.AddSingleton(_linkMapService);
        Services.AddSingleton(_topologySnapshotBuilder);
        Services.AddSingleton(_excelExportService);
        Services.AddSingleton<ToastService>();

        ComponentFactories.AddStub<LinkMapGroupDrawer>();
    }

    [Fact]
    public void Render_EmptyMap_ShowsEmptyStateAndLoadsMap()
    {
        _topologySnapshotBuilder.GetStateAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new TopologySnapshotStateDto(
                HasSnapshot: true,
                IsStale: false,
                IsBuilding: false,
                Status: "completed",
                LastCompletedAt: new DateTime(2026, 03, 28, 12, 0, 0, DateTimeKind.Utc),
                GroupCount: 0,
                ErrorMessage: null));

        _linkMapService.BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new LinkMapDto([]));

        var cut = Render<LinkMapPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("За поточним періодом груп не знайдено.");
            cut.Find("button[aria-label='Оновити карту']").Should().NotBeNull();
            cut.Markup.Should().Contain("Груп:");
        });

        _topologySnapshotBuilder.Received(1).GetStateAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
        _linkMapService.Received(1).BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Render_MapWithBridge_ShowsBridgePrimaryActionInFocusedPanel()
    {
        var groupBKey = "GROUP-B";
        var groupAKey = "GROUP-A";
        var now = new DateTime(2026, 03, 28, 12, 0, 0, DateTimeKind.Utc);

        _topologySnapshotBuilder.GetStateAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new TopologySnapshotStateDto(
                HasSnapshot: true,
                IsStale: false,
                IsBuilding: false,
                Status: "completed",
                LastCompletedAt: now,
                GroupCount: 2,
                ErrorMessage: null));

        var groupB = new LinkMapGroupDto(
            groupBKey,
            "Батальйон Б",
            ["145.1000"],
            "Б-ЦЕНТР",
            "координатор",
            ["Б-ЦЕНТР", "Б-1", "Б-2"],
            [
                new LinkMapMemberDto("Б-ЦЕНТР", "координатор", 3, 2, 4, now, 1, false, true),
                new LinkMapMemberDto("Б-1", "оператор", 1, 1, 1, now, 1, false, false),
                new LinkMapMemberDto("Б-2", "оператор", 1, 1, 1, now, 1, false, false)
            ],
            3,
            6,
            2,
            "нанесення ураження",
            ["нанесення ураження"],
            []);

        var groupA = new LinkMapGroupDto(
            groupAKey,
            "Батальйон А",
            ["402.0000"],
            "А-ЦЕНТР",
            "координатор",
            ["А-ЦЕНТР", "А-1", "А-2"],
            [
                new LinkMapMemberDto("А-ЦЕНТР", "координатор", 3, 2, 4, now, 1, false, true),
                new LinkMapMemberDto("А-1", "оператор", 1, 1, 1, now, 1, false, false),
                new LinkMapMemberDto("А-2", "оператор", 1, 1, 1, now, 1, false, false)
            ],
            3,
            6,
            2,
            "координація переміщення піхоти",
            ["координація переміщення піхоти"],
            [
                new LinkMapBridgeDto(
                    groupBKey,
                    "Батальйон Б",
                    "Б-ЦЕНТР",
                    "401.2000",
                    2,
                    "розвід інформація",
                    ["розвід інформація"])
            ]);

        _linkMapService.BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new LinkMapDto([groupA, groupB]));

        var cut = Render<LinkMapPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("розвід інформація");
            cut.Markup.Should().Contain("А-ЦЕНТР");
            cut.Markup.Should().Contain("Б-ЦЕНТР");
            cut.Markup.Should().Contain("401.2000");
        });
    }

    [Fact]
    public void Render_StaleSnapshot_ShowsWarningBannerAndStatus()
    {
        var now = new DateTime(2026, 03, 28, 12, 0, 0, DateTimeKind.Utc);

        _topologySnapshotBuilder.GetStateAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new TopologySnapshotStateDto(
                HasSnapshot: true,
                IsStale: true,
                IsBuilding: false,
                Status: "completed",
                LastCompletedAt: now,
                GroupCount: 1,
                ErrorMessage: null));

        _linkMapService.BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new LinkMapDto([
                new LinkMapGroupDto(
                    "GROUP-A",
                    null,
                    ["402.0000"],
                    "А-ЦЕНТР",
                    "координатор",
                    ["А-ЦЕНТР", "А-1"],
                    [
                        new LinkMapMemberDto("А-ЦЕНТР", "координатор", 2, 1, 2, now, 1, false, true),
                        new LinkMapMemberDto("А-1", "оператор", 1, 1, 1, now, 1, false, false)
                    ],
                    2,
                    2,
                    0,
                    "доповідь",
                    ["доповідь"],
                    [])
            ]));

        var cut = Render<LinkMapPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Перехвати змінені після останньої перебудови карти.");
            cut.Markup.Should().Contain("застарілий snapshot");
            cut.FindAll("button").Any(x => x.TextContent.Contains("Перебудувати")).Should().BeTrue();
        });
    }

    [Fact]
    public void RebuildButton_Click_RebuildsSnapshotAndReloadsMap()
    {
        var now = new DateTime(2026, 03, 28, 12, 0, 0, DateTimeKind.Utc);

        _topologySnapshotBuilder.GetStateAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(
                new TopologySnapshotStateDto(
                    HasSnapshot: false,
                    IsStale: false,
                    IsBuilding: false,
                    Status: "missing",
                    LastCompletedAt: null,
                    GroupCount: 0,
                    ErrorMessage: null),
                new TopologySnapshotStateDto(
                    HasSnapshot: true,
                    IsStale: false,
                    IsBuilding: false,
                    Status: "completed",
                    LastCompletedAt: now,
                    GroupCount: 0,
                    ErrorMessage: null));

        _topologySnapshotBuilder.RebuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new TopologySnapshotRebuildResultDto(
                Guid.NewGuid(),
                GroupCount: 0,
                CompletedAtUtc: now));

        _linkMapService.BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new LinkMapDto([]));

        var cut = Render<LinkMapPage>();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Для поточного періоду snapshot ще не побудований."));

        var rebuildButton = cut.Find("button[aria-label='Рефреш']");
        rebuildButton.Click();

        cut.WaitForAssertion(() =>
        {
            _topologySnapshotBuilder.Received(1).RebuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
            _topologySnapshotBuilder.Received(2).GetStateAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
            _linkMapService.Received(2).BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
        });
    }
}
