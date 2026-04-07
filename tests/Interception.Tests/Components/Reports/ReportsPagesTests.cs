//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.UI.Application.Exports.Abstractions;
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
    private readonly IExcelExportService _excelExportService;
    private readonly IPdfExportService _pdfExportService;

    public DivisionReportPageTests()
    {
        _divisionReportService = Substitute.For<IDivisionReportService>();
        _excelExportService = Substitute.For<IExcelExportService>();
        _pdfExportService = Substitute.For<IPdfExportService>();

        Services.AddSingleton(_divisionReportService);
        Services.AddSingleton(_excelExportService);
        Services.AddSingleton(_pdfExportService);
        Services.AddSingleton<ToastService>();
    }

    [Fact]
    public void Render_EmptyData_ShowsEmptyStateAndLoadsData()
    {
        _divisionReportService.BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new DivisionReportDto([]));

        var cut = Render<DivisionReportPage>();

        cut.Markup.Should().Contain("Даних для звіту немає");
        cut.Markup.Should().Contain("Останні 7 днів");

        _divisionReportService.Received(1).BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }
}

public sealed class DayPicturePageTests : BunitContext
{
    private readonly IDayPictureService _dayPictureService;
    private readonly IExcelExportService _excelExportService;
    private readonly IPdfExportService _pdfExportService;

    public DayPicturePageTests()
    {
        _dayPictureService = Substitute.For<IDayPictureService>();
        _excelExportService = Substitute.For<IExcelExportService>();
        _pdfExportService = Substitute.For<IPdfExportService>();

        Services.AddSingleton(_dayPictureService);
        Services.AddSingleton(_excelExportService);
        Services.AddSingleton(_pdfExportService);
        Services.AddSingleton<ToastService>();
    }

    [Fact]
    public void Render_EmptyData_ShowsEmptyStateAndLoadsData()
    {
        _dayPictureService.BuildAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new DayPictureDto(DateTime.Today, 0, []));

        var cut = Render<DayPicturePage>();

        cut.Markup.Should().Contain("За вибраний день спостережень не знайдено.");
        cut.Markup.Should().Contain("Сьогодні");

        _dayPictureService.Received(1).BuildAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }
}
