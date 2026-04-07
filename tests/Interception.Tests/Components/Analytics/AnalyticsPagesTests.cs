//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Exports.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Analytics.Candidates;
using Interception.UI.Components.Pages.Analytics.Candidates.Drawers;
using Interception.UI.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Interception.Tests.Components.Analytics;

public sealed class CandidatesPageTests : BunitContext
{
    private readonly IParticipantCandidateGroupQueryService _queryService;
    private readonly IParticipantCandidateAnalysisService _analysisService;
    private readonly IPdfExportService _pdfExportService;

    public CandidatesPageTests()
    {
        _queryService = Substitute.For<IParticipantCandidateGroupQueryService>();
        _analysisService = Substitute.For<IParticipantCandidateAnalysisService>();
        _pdfExportService = Substitute.For<IPdfExportService>();

        Services.AddSingleton(_queryService);
        Services.AddSingleton(_analysisService);
        Services.AddSingleton(_pdfExportService);
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
