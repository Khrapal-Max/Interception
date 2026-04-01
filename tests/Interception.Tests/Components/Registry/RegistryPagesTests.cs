//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.Application.Analytics.Dtos;
using Interception.Application.Registry.Abstractions;
using Interception.Application.Registry.Dtos;
using Interception.Application.Toasts;
using Interception.Web.UI.Components.Pages.Registry.Divisions;
using Interception.Web.UI.Components.Pages.Registry.Divisions.Drawers;
using Interception.Web.UI.Components.Pages.Registry.Persons;
using Interception.Web.UI.Components.Pages.Registry.Persons.Drawers;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Interception.Tests.Components.Registry;

public sealed class FrequencyDivisionRegistryTests : BunitContext
{
    private readonly IFrequencyDivisionService _frequencyDivisionService;

    public FrequencyDivisionRegistryTests()
    {
        _frequencyDivisionService = Substitute.For<IFrequencyDivisionService>();

        Services.AddSingleton(_frequencyDivisionService);
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

    public PersonsRegistryTests()
    {
        _personRegistryService = Substitute.For<IPersonRegistryService>();

        Services.AddSingleton(_personRegistryService);
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
