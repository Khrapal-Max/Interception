//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;

namespace Interception.Tests.Domain;

public sealed class ObservationActionTests
{
    [Fact]
    public void Create_SetsNormalizedFields_And_Defaults()
    {
        var action = ObservationAction.Create(
            "  Передача цілі  ",
            "  Ініціатор  ",
            "  Відповідач  ",
            "  Короткий опис  ",
            "  analyst  ");

        Assert.Equal("Передача цілі", action.Name);
        Assert.Equal("передача цілі", action.NameNorm);
        Assert.Equal("Ініціатор", action.InitiatorRoleName);
        Assert.Equal("Відповідач", action.ResponderRoleName);
        Assert.Equal("Короткий опис", action.Description);
        Assert.Equal("analyst", action.CreatedBy);
        Assert.True(action.IsActive);
    }

    [Fact]
    public void Create_Throws_When_Name_Is_Empty()
    {
        var ex = Assert.Throws<ArgumentException>(() => ObservationAction.Create("   "));
        Assert.Contains("Action name is required.", ex.Message);
    }

    [Fact]
    public void Rename_Updates_Name_And_Normalized_Name()
    {
        var action = ObservationAction.Create("Передача");

        action.Rename("  Підтвердження цілі  ");

        Assert.Equal("Підтвердження цілі", action.Name);
        Assert.Equal("підтвердження цілі", action.NameNorm);
    }

    [Fact]
    public void ConfigureRoles_And_SetDescription_Normalize_Optional_Values()
    {
        var action = ObservationAction.Create("Передача");

        action.ConfigureRoles("  Оператор  ", "   ");
        action.SetDescription("  Опис   ");

        Assert.Equal("Оператор", action.InitiatorRoleName);
        Assert.Null(action.ResponderRoleName);
        Assert.Equal("Опис", action.Description);
    }

    [Fact]
    public void Archive_And_Restore_Toggle_IsActive()
    {
        var action = ObservationAction.Create("Передача");

        action.Archive();
        Assert.False(action.IsActive);

        action.Restore();
        Assert.True(action.IsActive);
    }
}
