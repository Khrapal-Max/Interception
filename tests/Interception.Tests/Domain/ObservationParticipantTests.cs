//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;

namespace Interception.Tests.Domain;

public sealed class ObservationParticipantTests
{
    [Fact]
    public void UpdateParticipant_To_Unknown_Sets_StartedAsUnknown_True_For_Analytics_History()
    {
        var observation = Observation.Create(new DateTime(2026, 3, 20, 6, 40, 0, DateTimeKind.Utc), "Рух");
        var participant = observation.AddParticipant("Клим", isUnknown: false, roleRaw: "водій");

        observation.UpdateParticipant(participant.Id, "Клим", isUnknown: true, roleRaw: "водій");

        var updated = Assert.Single(observation.Participants);
        Assert.True(updated.IsUnknown);
        Assert.True(updated.StartedAsUnknown); // flips once record becomes unknown again
    }

    [Fact]
    public void UpdateParticipant_Can_Clear_Label_And_Becomes_Unknown()
    {
        var observation = Observation.Create(new DateTime(2026, 3, 20, 6, 40, 0, DateTimeKind.Utc), "Рух");
        var participant = observation.AddParticipant("Клим", isUnknown: false, roleRaw: "водій");

        observation.UpdateParticipant(participant.Id, "   ", isUnknown: false, roleRaw: "стрілець");

        var updated = Assert.Single(observation.Participants);
        Assert.True(updated.IsUnknown);
        Assert.Null(updated.LabelRaw);
        Assert.Null(updated.LabelNorm);
        Assert.Equal("стрілець", updated.RoleRaw);
    }
}
