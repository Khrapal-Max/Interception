//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.UI.Components.Pages.Analytics.Toolbar;
using Microsoft.Extensions.DependencyInjection;

namespace Interception.Tests.Components.Analytics;

public sealed class CandidatesToolbarTests : BunitContext
{
    [Fact]
    public void Render_Default_RendersTwoNavigationLinks()
    {
        var cut = Render<CandidatesToolbar>();

        var links = cut.FindAll("a.registry-nav-link");
        links.Should().HaveCount(4);
        links.Select(l => l.GetAttribute("href")).Should().Contain(["/analytics/link-map", "/analytics/frequency-weights"]);
    }

    [Fact]
    public void Render_WithMetaAndActions_RendersBothFragments()
    {
        var cut = Render<CandidatesToolbar>(p => p
            .Add(x => x.MetaContent, b => b.AddContent(0, "META"))
            .Add(x => x.ActionsContent, b => b.AddContent(0, "ACTION")));

        cut.Markup.Should().Contain("META");
        cut.Markup.Should().Contain("ACTION");
    }
}
