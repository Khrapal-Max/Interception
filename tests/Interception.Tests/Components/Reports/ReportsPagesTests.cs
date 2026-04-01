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
    [Fact]
    public void Render_EmptyData_ShowsEmptyStateAndLoadsData()
    {
        var service = Substitute.For<IDivisionReportService>();
        service.BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new DivisionReportDto([]));

        Services.AddSingleton(service);
        Services.AddSingleton<ToastService>();

        var cut = Render<DivisionReportPage>();

        cut.Markup.Should().Contain("Даних для звіту немає");
        cut.Markup.Should().Contain("Останні 7 днів");

        service.Received(1).BuildAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }
}

public sealed class DayPicturePageTests : BunitContext
{
    [Fact]
    public void Render_EmptyData_ShowsEmptyStateAndLoadsData()
    {
        var service = Substitute.For<IDayPictureService>();
        service.BuildAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new DayPictureDto(DateOnly.FromDateTime(DateTime.Today), 0, []));

        Services.AddSingleton(service);
        Services.AddSingleton<ToastService>();

        var cut = Render<DayPicturePage>();

        cut.Markup.Should().Contain("За вибраний день спостережень не знайдено.");
        cut.Markup.Should().Contain("Сьогодні");

        service.Received(1).BuildAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }
}
