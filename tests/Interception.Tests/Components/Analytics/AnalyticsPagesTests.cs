//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Interception.Application.Analytics.Abstractions;
using Interception.Application.Analytics.Dtos;
using Interception.Application.Interceptions.Dtos;
using Interception.Application.Toasts;
using Interception.Web.UI.Components.Pages.Analytics.Candidates;
using Interception.Web.UI.Components.Pages.Analytics.Candidates.Drawers;
using Interception.Web.UI.Components.Pages.Analytics.LinkMap;
using Interception.Web.UI.Components.Pages.Analytics.LinkMap.Drawers;
using Interception.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Interception.Tests.Components.Analytics;

public sealed class CandidatesPageTests : BunitContext
{
    private readonly IParticipantCandidateGroupQueryService _queryService;
    private readonly IParticipantCandidateAnalysisService _analysisService;

    public CandidatesPageTests()
    {
        _queryService = Substitute.For<IParticipantCandidateGroupQueryService>();
        _analysisService = Substitute.For<IParticipantCandidateAnalysisService>();

        Services.AddSingleton(_queryService);
        Services.AddSingleton(_analysisService);
        Services.AddSingleton<ToastService>();

        ComponentFactories.AddStub<CandidateGroupDrawer>();
    }

    [Fact]
    public void Render_EmptyData_ShowsEmptyStateAndLoadsData()
    {
        _queryService.GetGroupsByStatusAsync(Arg.Any<CandidateGroupStatus>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResultDto<CandidateGroupDto>([], 0, 1, 50));

        _analysisService.RunAsync(Arg.Any<CancellationToken>()).Returns(0);

        var cut = Render<CandidatesPage>();

        cut.Markup.Should().Contain("Груп для розгляду немає");
        cut.Markup.Should().Contain("Запустити аналіз");

        _queryService.Received(1).GetGroupsByStatusAsync(CandidateGroupStatus.Open, 1, 1, Arg.Any<CancellationToken>());
        _queryService.Received(1).GetGroupsByStatusAsync(CandidateGroupStatus.Open, 1, 50, Arg.Any<CancellationToken>());
    }
}

public sealed class LinkMapPageTests : BunitContext
{
    private readonly ILinkMapService _linkMapService;

    public LinkMapPageTests()
    {
        _linkMapService = Substitute.For<ILinkMapService>();

        Services.AddSingleton(_linkMapService);
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
}
