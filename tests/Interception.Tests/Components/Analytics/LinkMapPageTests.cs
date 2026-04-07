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
    private readonly IExcelExportService _excelExportService;
    private readonly IPdfExportService _pdfExportService;

    public LinkMapPageTests()
    {
        _linkMapService = Substitute.For<ILinkMapService>();
        _excelExportService = Substitute.For<IExcelExportService>();
        _pdfExportService = Substitute.For<IPdfExportService>();

        Services.AddSingleton(_linkMapService);
        Services.AddSingleton(_excelExportService);
        Services.AddSingleton(_pdfExportService);
        Services.AddSingleton<ToastService>();

        ComponentFactories.AddStub<LinkMapGroupDrawer>();
    }

    [Fact]
    public void Render_EmptyMap_ShowsEmptyStateAndLoadsMap()
    {
        _linkMapService.BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new LinkMapDto([]));

        var cut = Render<LinkMapPage>();

        cut.Markup.Should().Contain("За поточним періодом груп не знайдено.");
        cut.Find("button.btn.btn-primary").TextContent.Should().Contain("Оновити");

        _linkMapService.Received(1).BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Render_MapWithBridge_ShowsBridgePrimaryActionInFocusedPanel()
    {
        var groupBKey = "GROUP-B";
        var groupAKey = "GROUP-A";
        var now = new DateTime(2026, 03, 28, 12, 0, 0, DateTimeKind.Utc);

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

        cut.Markup.Should().Contain("розвід інформація");
        cut.Markup.Should().Contain("А-ЦЕНТР");
        cut.Markup.Should().Contain("Б-ЦЕНТР");
    }
}
