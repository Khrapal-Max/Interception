//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Interception.UI.Application.Analytics.Dtos;
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
    [Fact]
    public void Render_EmptyData_ShowsEmptyStateAndLoadsData()
    {
        var service = Substitute.For<IFrequencyDivisionService>();
        service.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<FrequencySuggestionDto>());

        Services.AddSingleton(service);
        Services.AddSingleton<ToastService>();
        ComponentFactories.AddStub<FrequencyDivisionDrawer>();

        var cut = RenderComponent<FrequencyDivisionRegistry>();

        cut.Markup.Should().Contain("Частот ще немає");
        cut.Markup.Should().Contain("Оновити");

        service.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }
}

public sealed class PersonsRegistryTests : BunitContext
{
    [Fact]
    public void Render_EmptyData_ShowsEmptyStateAndLoadsData()
    {
        var service = Substitute.For<IPersonRegistryService>();
        service.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<PersonRegistryItemDto>());

        Services.AddSingleton(service);
        Services.AddSingleton<ToastService>();
        ComponentFactories.AddStub<PersonRegistryDrawer>();

        var cut = RenderComponent<PersonsRegistry>();

        cut.Markup.Should().Contain("Осіб у реєстрі ще немає");
        cut.Markup.Should().Contain("Оновити");

        service.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }
}
