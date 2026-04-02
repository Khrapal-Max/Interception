//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Exports.Abstractions;
using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Registry.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Registry.Divisions;
using Interception.UI.Components.Pages.Registry.Divisions.Drawers;
using Interception.UI.Components.Pages.Registry.Persons;
using Interception.UI.Components.Pages.Registry.Persons.Drawers;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Interception.Tests.Components.Registry;

public sealed class FrequencyDivisionRegistryTests : BunitContext
{
    private readonly IFrequencyDivisionService _frequencyDivisionService;
    private readonly IExcelExportService _excelExportService;

    public FrequencyDivisionRegistryTests()
    {
        _frequencyDivisionService = Substitute.For<IFrequencyDivisionService>();
        _excelExportService = Substitute.For<IExcelExportService>();

        Services.AddSingleton(_frequencyDivisionService);
        Services.AddSingleton(_excelExportService);
        Services.AddSingleton<ToastService>();

        ComponentFactories.AddStub<FrequencyDivisionDrawer>();
    }

    [Fact]
    public void Render_EmptyData_ShowsEmptyStateAndLoadsData()
    {
        _frequencyDivisionService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<FrequencySuggestionDto>());

        var cut = Render<FrequencyDivisionRegistry>();

        cut.Markup.Should().Contain("Частот ще немає");
        cut.Markup.Should().Contain("Оновити");

        _frequencyDivisionService.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }
}

public sealed class PersonsRegistryTests : BunitContext
{
    private readonly IPersonRegistryService _personRegistryService;
    private readonly IExcelExportService _excelExportService;

    public PersonsRegistryTests()
    {
        _personRegistryService = Substitute.For<IPersonRegistryService>();
        _excelExportService = Substitute.For<IExcelExportService>();

        Services.AddSingleton(_personRegistryService);
        Services.AddSingleton(_excelExportService);
        Services.AddSingleton<ToastService>();

        ComponentFactories.AddStub<PersonRegistryDrawer>();
    }

    [Fact]
    public void Render_EmptyData_ShowsEmptyStateAndLoadsData()
    {
        _personRegistryService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<PersonRegistryItemDto>());

        var cut = Render<PersonsRegistry>();

        cut.Markup.Should().Contain("Осіб у реєстрі ще немає");
        cut.Markup.Should().Contain("Оновити");

        _personRegistryService.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }
}
