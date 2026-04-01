//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.UI.Application.Reports.Abstractions;
using Interception.UI.Application.Reports.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Reports.DayPicture;
using Interception.UI.Components.Pages.Reports.Divisions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Interception.Tests.Components.Reports;

public sealed class DivisionReportPageTests : BunitContext
{
    private readonly IDivisionReportService _divisionReportService;

    public DivisionReportPageTests()
    {
        _divisionReportService = Substitute.For<IDivisionReportService>();

        Services.AddSingleton(_divisionReportService);
        Services.AddSingleton<ToastService>();
    }

    [Fact]
    public void Render_EmptyData_ShowsEmptyStateAndLoadsData()
    {
        _divisionReportService.BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new DivisionReportDto([]));

        var cut = RenderComponent<DivisionReportPage>();

        cut.Markup.Should().Contain("Даних для звіту немає");
        cut.Markup.Should().Contain("Останні 7 днів");

        _divisionReportService.Received(1).BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }
}

public sealed class DayPicturePageTests : BunitContext
{
    private readonly IDayPictureService _dayPictureService;

    public DayPicturePageTests()
    {
        _dayPictureService = Substitute.For<IDayPictureService>();

        Services.AddSingleton(_dayPictureService);
        Services.AddSingleton<ToastService>();
    }

    [Fact]
    public void Render_EmptyData_ShowsEmptyStateAndLoadsData()
    {
        _dayPictureService.BuildAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new DayPictureDto(DateOnly.FromDateTime(DateTime.Today), 0, []));

        var cut = RenderComponent<DayPicturePage>();

        cut.Markup.Should().Contain("За вибраний день спостережень не знайдено.");
        cut.Markup.Should().Contain("Сьогодні");

        _dayPictureService.Received(1).BuildAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }
}
