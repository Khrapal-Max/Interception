//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Interception.UI.Domain.Enums;

namespace Interception.Tests.Domain;

public sealed class TagCatalogTests
{
    [Fact]
    public void Create_Rename_ChangeKind_And_Archive_Work()
    {
        var catalog = TagCatalog.Create("  ДРГ  ", TagKind.Keyword, createdBy: "  analyst  ");

        catalog.Rename("  Водій  ");
        catalog.ChangeKind(TagKind.Person);
        catalog.Archive();
        catalog.Restore();

        Assert.Equal("Водій", catalog.Name);
        Assert.Equal("водій", catalog.NameNorm);
        Assert.Equal(TagKind.Person, catalog.Kind);
        Assert.True(catalog.IsActive);
        Assert.Equal("analyst", catalog.CreatedBy);
    }
}
