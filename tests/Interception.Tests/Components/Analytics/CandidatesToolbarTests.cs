//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.UI.Components.Pages.Analytics.Toolbar;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Interception.Tests.Components.Analytics;

public sealed class CandidatesToolbarTests : BunitContext
{
    [Fact]
    public void Render_Default_RendersTwoNavigationLinks()
    {
        var cut = Render<CandidatesToolbar>();

        var links = cut.FindAll("a.registry-nav-link");
        links.Should().HaveCount(2);
        links.Select(l => l.GetAttribute("href")).Should().Contain(["/analytics/candidates", "/analytics/link-map"]);
    }

    [Fact]
    public void ActiveSection_Groups_UsesExactMatchingForGroupsLink()
    {
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("http://localhost/analytics/candidates/archive");

        var active = Render<CandidatesToolbar>(p => p
            .Add(x => x.ActiveSection, "groups"))
            .Find("a[href='/analytics/candidates']");

        var nonActive = Render<CandidatesToolbar>()
            .Find("a[href='/analytics/candidates']");

        active.ClassList.Should().NotContain("active");
        nonActive.ClassList.Should().Contain("active");
    }

   /* [Fact]
    public void Render_WithMetaAndActions_RendersBothFragments()
    {
        var cut = RenderComponent<CandidatesToolbar>(p => p
            .Add<RenderFragment>(x => x.MetaContent, _ => b => b.AddContent(0, "META"))
            .Add<RenderFragment>(x => x.ActionsContent, _ => b => b.AddContent(0, "ACTION")));

        cut.Markup.Should().Contain("META");
        cut.Markup.Should().Contain("ACTION");
    }*/
}
