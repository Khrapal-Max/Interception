//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Interception.UI.Domain.Enums;

namespace Interception.Tests.Domain;

public sealed class ObservationActionTests
{
    [Fact]
    public void Create_Sets_Reference_Data_And_Normalized_Name()
    {
        var action = ObservationAction.Create(
            "  Передача наказу  ",
            category: ObservationActionCategory.Command,
            initiatorRoleName: "  командир  ",
            responderRoleName: "  підлеглий  ",
            description: "  короткий опис  ",
            typicalParticipantsCount: 2,
            requiresCounterparty: true,
            createdBy: "  analyst  ");

        Assert.Equal("Передача наказу", action.Name);
        Assert.Equal("передача наказу", action.NameNorm);
        Assert.Equal(ObservationActionCategory.Command, action.Category);
        Assert.Equal("командир", action.InitiatorRoleName);
        Assert.Equal("підлеглий", action.ResponderRoleName);
        Assert.Equal("короткий опис", action.Description);
        Assert.Equal((short)2, action.TypicalParticipantsCount);
        Assert.True(action.RequiresCounterparty);
        Assert.True(action.IsActive);
        Assert.Equal("analyst", action.CreatedBy);
    }

    [Fact]
    public void Rename_And_Reconfigure_Update_Action_Metadata()
    {
        var action = ObservationAction.Create("Рух");

        action.Rename("  Вихід на рубіж  ");
        action.SetCategory(ObservationActionCategory.Movement);
        action.ConfigureRoles("  водій  ", "  командир  ");
        action.ConfigureUsage(3, requiresCounterparty: false);
        action.SetDescription("  оновлений опис  ");
        action.Archive();
        action.Restore();

        Assert.Equal("Вихід на рубіж", action.Name);
        Assert.Equal("вихід на рубіж", action.NameNorm);
        Assert.Equal(ObservationActionCategory.Movement, action.Category);
        Assert.Equal("водій", action.InitiatorRoleName);
        Assert.Equal("командир", action.ResponderRoleName);
        Assert.Equal((short)3, action.TypicalParticipantsCount);
        Assert.False(action.RequiresCounterparty);
        Assert.Equal("оновлений опис", action.Description);
        Assert.True(action.IsActive);
    }

    [Fact]
    public void Create_Rejects_Invalid_Usage_Count()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ObservationAction.Create("Рух", typicalParticipantsCount: 0));
    }

    [Fact]
    public void ConfigureUsage_Rejects_Invalid_Usage_Count()
    {
        var action = ObservationAction.Create("Рух");

        Assert.Throws<ArgumentOutOfRangeException>(() => action.ConfigureUsage(0, requiresCounterparty: true));
    }
}
