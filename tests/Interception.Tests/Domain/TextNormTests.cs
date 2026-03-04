//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.Tests.Domain;

public class TextNormTests
{
    [Fact]
    public void Normalize_Returns_Null_For_Empty()
    {
        Assert.Null(TextNorm.Normalize(null));
        Assert.Null(TextNorm.Normalize(""));
        Assert.Null(TextNorm.Normalize("   "));
    }

    [Fact]
    public void Normalize_Trims_Collapses_Whitespace_And_Lowers()
    {
        var v = TextNorm.Normalize("  A   B\nC  ");
        Assert.Equal("a b c", v);
    }

    [Fact]
    public void NormalizeRequired_Throws_For_Empty()
    {
        Assert.Throws<ArgumentException>(() => TextNorm.NormalizeRequired("  "));
    }
}
