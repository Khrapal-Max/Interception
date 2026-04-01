//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.UI.Components.Pages.Reports.Toolbar;
using Microsoft.AspNetCore.Components;

namespace Interception.Tests.Components.Reports;

public sealed class ReportsToolbarTests : BunitContext
{
    [Fact]
    public void Render_Default_RendersTwoNavigationButtons()
    {
        var cut = Render<ReportsToolbar>();

        var links = cut.FindAll(".toolbar-start a");
        links.Should().HaveCount(2);
    }

    [Fact]
    public void ActiveSection_Divisions_MarksDivisionsButtonAsPrimary()
    {
        var cut = Render<ReportsToolbar>(p => p
            .Add(x => x.ActiveSection, "divisions"));

        var divisions = cut.Find("a[href='/reports/divisions']");
        var dayPicture = cut.Find("a[href='/reports/day-picture']");

        divisions.ClassList.Should().Contain("btn-success");
        dayPicture.ClassList.Should().Contain("btn-outline-secondary");
    }

  /*  [Fact]
    public void Render_WithMetaAndActions_RendersBothFragments()
    {
        var cut = RenderComponent<ReportsToolbar>(p => p
            .Add<RenderFragment>(x => x.MetaContent, _ => b => b.AddContent(0, "META"))
            .Add<RenderFragment>(x => x.ActionsContent, _ => b => b.AddContent(0, "ACTION")));

        cut.Markup.Should().Contain("META");
        cut.Markup.Should().Contain("ACTION");
    }*/
}
