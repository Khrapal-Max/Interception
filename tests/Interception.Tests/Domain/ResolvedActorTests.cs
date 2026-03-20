//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;

namespace Interception.Tests.Domain;

public sealed class ResolvedActorTests
{
    [Fact]
    public void Create_And_Update_Metadata_Work()
    {
        var actor = ResolvedActor.Create("  Клим  ", primaryRole: "  водій  ", note: "  примітка  ", createdBy: "  analyst  ");

        actor.Rename("  Клименко  ");
        actor.UpdatePrimaryRole("  командир  ");
        actor.SetNote("  оновлено  ");
        actor.Archive();
        actor.Restore();

        Assert.Equal("Клименко", actor.DisplayName);
        Assert.Equal("клименко", actor.DisplayNameNorm);
        Assert.Equal("командир", actor.PrimaryRole);
        Assert.Equal("оновлено", actor.Note);
        Assert.True(actor.IsActive);
        Assert.Equal("analyst", actor.CreatedBy);
    }
}
