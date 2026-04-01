//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.UI.Components.Pages.Registry.Toolbar;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Interception.Tests.Components.Registry;

public sealed class RegistriesToolbarTests : BunitContext
{
    [Fact]
    public void Render_Default_RendersThreeNavigationLinks()
    {
        var cut = Render<RegistriesToolbar>();

        var links = cut.FindAll("a.registry-nav-link");
        links.Should().HaveCount(3);
    }

    [Fact]
    public void ActiveSection_Divisions_UsesExactMatchingForDivisionsLink()
    {
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/registries/divisions/archive");

        var active = Render<RegistriesToolbar>(p => p
            .Add(x => x.ActiveSection, "divisions"))
            .Find("a[href='/registries/divisions']");

        var nonActive = Render<RegistriesToolbar>()
            .Find("a[href='/registries/divisions']");

        active.ClassList.Should().NotContain("active");
        nonActive.ClassList.Should().Contain("active");
    }

    [Fact]
    public void Render_WithMetaAndActions_RendersBothFragments()
    {
        var cut = Render<RegistriesToolbar>(p => p
            .Add(x => x.MetaContent, (RenderFragment)(b => b.AddContent(0, "META")))
            .Add(x => x.ActionsContent, (RenderFragment)(b => b.AddContent(0, "ACTION"))));

        cut.Markup.Should().Contain("META");
        cut.Markup.Should().Contain("ACTION");
    }
}
