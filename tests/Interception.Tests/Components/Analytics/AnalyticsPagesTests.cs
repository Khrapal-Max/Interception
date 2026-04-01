//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Analytics.Candidates;
using Interception.UI.Components.Pages.Analytics.Candidates.Drawers;
using Interception.UI.Components.Pages.Analytics.LinkMap;
using Interception.UI.Components.Pages.Analytics.LinkMap.Drawers;
using Interception.UI.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Interception.Tests.Components.Analytics;

public sealed class CandidatesPageTests : BunitContext
{
    [Fact]
    public void Render_EmptyData_ShowsEmptyStateAndLoadsData()
    {
        var query = Substitute.For<IParticipantCandidateGroupQueryService>();
        query.GetGroupsByStatusAsync(Arg.Any<CandidateGroupStatus>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
             .Returns(new PagedResultDto<CandidateGroupDto>([], 0, 1, 50));

        var analysis = Substitute.For<IParticipantCandidateAnalysisService>();
        analysis.RunAsync(Arg.Any<CancellationToken>()).Returns(0);

        Services.AddSingleton(query);
        Services.AddSingleton(analysis);
        Services.AddSingleton<ToastService>();
        ComponentFactories.AddStub<CandidateGroupDrawer>();

        var cut = RenderComponent<CandidatesPage>();

        cut.Markup.Should().Contain("Груп для розгляду немає");
        cut.Markup.Should().Contain("Запустити аналіз");

        query.Received(1).GetGroupsByStatusAsync(CandidateGroupStatus.Open, 1, 1, Arg.Any<CancellationToken>());
        query.Received(1).GetGroupsByStatusAsync(CandidateGroupStatus.Open, 1, 50, Arg.Any<CancellationToken>());
    }
}

public sealed class LinkMapPageTests : BunitContext
{
    [Fact]
    public void Render_EmptyMap_ShowsEmptyStateAndLoadsMap()
    {
        var service = Substitute.For<ILinkMapService>();
        service.BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new LinkMapDto([]));

        Services.AddSingleton(service);
        Services.AddSingleton<ToastService>();
        ComponentFactories.AddStub<LinkMapGroupDrawer>();

        var cut = RenderComponent<LinkMapPage>();

        cut.Markup.Should().Contain("За поточним періодом груп не знайдено.");
        cut.Find("button.btn.btn-primary").TextContent.Should().Contain("Оновити");

        service.Received(1).BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }
}
