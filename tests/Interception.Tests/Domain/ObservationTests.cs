//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Interception.UI.Domain.Enums;

namespace Interception.Tests.Domain;

public sealed class ObservationTests
{
    [Fact]
    public void Create_Trims_Fields_And_Computes_Hash()
    {
        var observation = Observation.Create(
            new DateOnly(2026, 3, 14),
            DayPart.FirstHalf,
            "  Передача цілі  ",
            layer: "  5  ",
            rmRaw: "  РМ-12  ",
            pointRaw: "  Точка-1  ",
            locationRaw: "  Позиція  ",
            districtRaw: "  Район  ",
            note: "  Примітка  ",
            source: "  import  ",
            sourceRow: 17,
            createdBy: "  operator  ");

        Assert.Equal(new DateOnly(2026, 3, 14), observation.ObservedDate);
        Assert.Equal(DayPart.FirstHalf, observation.DayPart);
        Assert.Equal("Передача цілі", observation.ActionRaw);
        Assert.Equal("передача цілі", observation.ActionNorm);
        Assert.Equal("5", observation.Layer);
        Assert.Equal("РМ-12", observation.RmRaw);
        Assert.Equal("Точка-1", observation.PointRaw);
        Assert.Equal("Позиція", observation.LocationRaw);
        Assert.Equal("Район", observation.DistrictRaw);
        Assert.Equal("Примітка", observation.Note);
        Assert.Equal("import", observation.Source);
        Assert.Equal(17, observation.SourceRow);
        Assert.Equal("operator", observation.CreatedBy);
        Assert.False(string.IsNullOrWhiteSpace(observation.ContentHash));
    }

    [Fact]
    public void AddParticipant_Throws_For_Duplicate_Known_Label_In_Same_Observation()
    {
        var observation = CreateObservation();
        observation.AddParticipant("КЛИМ", isUnknown: false, roleRaw: "водій", ordinal: 1);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            observation.AddParticipant("  клим  ", isUnknown: false, roleRaw: "інша роль", ordinal: 2));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddParticipant_Allows_Multiple_Unknowns_Even_With_Similar_Labels()
    {
        var observation = CreateObservation();

        observation.AddParticipant("НВ 1", isUnknown: true, roleRaw: "водій", ordinal: 1);
        observation.AddParticipant("НВ 1", isUnknown: true, roleRaw: "водій", ordinal: 2);

        Assert.Equal(2, observation.Participants.Count);
        Assert.All(observation.Participants, x => Assert.True(x.IsUnknown));
    }

    [Fact]
    public void ContentHash_Does_Not_Depend_On_Unknown_Label_Text()
    {
        var first = CreateObservation();
        first.AddParticipant("НВ 1", isUnknown: true, roleRaw: "водій", ordinal: 1);

        var second = CreateObservation();
        second.AddParticipant("НВ із складу 656 мсп", isUnknown: true, roleRaw: "водій", ordinal: 1);

        Assert.Equal(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void UpdateParticipant_From_Unknown_To_Known_Preserves_StartedAsUnknown()
    {
        var observation = CreateObservation();
        var participant = observation.AddParticipant(null, isUnknown: true, roleRaw: "водій", ordinal: 1);

        observation.UpdateParticipant(participant.Id, "КЛИМ", isUnknown: false, roleRaw: "старший водій");

        Assert.False(participant.IsUnknown);
        Assert.True(participant.StartedAsUnknown);
        Assert.Equal("КЛИМ", participant.LabelRaw);
        Assert.Equal("клим", participant.LabelNorm);
        Assert.Equal("старший водій", participant.RoleRaw);
    }

    [Fact]
    public void UpdateParticipant_To_Unknown_Sets_StartedAsUnknown()
    {
        var observation = CreateObservation();
        var participant = observation.AddParticipant("КЛИМ", isUnknown: false, roleRaw: "водій", ordinal: 1);

        observation.UpdateParticipant(participant.Id, null, isUnknown: true, roleRaw: "водій");

        Assert.True(participant.IsUnknown);
        Assert.True(participant.StartedAsUnknown);
        Assert.Null(participant.LabelRaw);
        Assert.Null(participant.LabelNorm);
    }

    [Fact]
    public void BindAction_Updates_Action_Id_And_Recomputes_Hash()
    {
        var observation = CreateObservation();
        var before = observation.ContentHash;
        var actionId = Guid.NewGuid();

        observation.BindAction(actionId, "  Підтвердження цілі  ");

        Assert.Equal(actionId, observation.ObservationActionId);
        Assert.Equal("Підтвердження цілі", observation.ActionRaw);
        Assert.Equal("підтвердження цілі", observation.ActionNorm);
        Assert.NotEqual(before, observation.ContentHash);
    }

    [Fact]
    public void UpdateContext_And_RemoveParticipant_Recompute_Hash()
    {
        var observation = CreateObservation();
        var participant = observation.AddParticipant("КЛИМ", isUnknown: false, roleRaw: "водій", ordinal: 1);
        var withParticipantHash = observation.ContentHash;

        observation.UpdateContext(
            actionRaw: "Нова дія",
            layer: "7",
            rmRaw: "РМ-77",
            pointRaw: "Точка-7",
            locationRaw: "Локація-7",
            districtRaw: "Район-7",
            note: "Оновлено");

        var afterContextHash = observation.ContentHash;
        Assert.NotEqual(withParticipantHash, afterContextHash);
        Assert.Equal("Нова дія", observation.ActionRaw);
        Assert.Equal("нова дія", observation.ActionNorm);

        observation.RemoveParticipant(participant.Id);

        Assert.Empty(observation.Participants);
        Assert.NotEqual(afterContextHash, observation.ContentHash);
    }

    [Fact]
    public void ClearBoundAction_Clears_Only_Foreign_Key()
    {
        var observation = CreateObservation();
        observation.BindAction(Guid.NewGuid(), "Підтвердження цілі");

        observation.ClearBoundAction();

        Assert.Null(observation.ObservationActionId);
        Assert.Equal("Підтвердження цілі", observation.ActionRaw);
        Assert.Equal("підтвердження цілі", observation.ActionNorm);
    }

    private static Observation CreateObservation()
        => Observation.Create(
            new DateOnly(2026, 3, 14),
            DayPart.FirstHalf,
            "Передача цілі",
            layer: "5",
            rmRaw: "РМ-12",
            pointRaw: "Точка-1",
            locationRaw: "Позиція",
            districtRaw: "Район",
            note: "Примітка");
}
