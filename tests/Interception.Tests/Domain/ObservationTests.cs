//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Interception.UI.Domain.Enums;

namespace Interception.Tests.Domain;

public sealed class ObservationTests
{
    [Fact]
    public void Create_Normalizes_Context_And_Subdivision()
    {
        var observedAt = new DateTime(2026, 3, 20, 14, 25, 0, DateTimeKind.Utc);

        var observation = Observation.Create(
            observedAt,
            "  Радіообмін  ",
            layer: "  656 мсп  ",
            rmRaw: "  Р-123  ",
            pointRaw: "  Точка 1  ",
            locationRaw: "  Північ  ",
            districtRaw: "  Район А  ",
            subdivisionRaw: "  2 мсб  ",
            subdivisionStrength: SubdivisionLinkStrength.Strong,
            subdivisionSource: ObservationSubdivisionSource.Note,
            note: "  Коротка примітка  ",
            createdBy: "  analyst  ");

        Assert.Equal(observedAt, observation.ObservedDate);
        Assert.Equal("Радіообмін", observation.ActionRaw);
        Assert.Equal("радіообмін", observation.ActionNorm);
        Assert.Equal("656 мсп", observation.Layer);
        Assert.Equal("Р-123", observation.RmRaw);
        Assert.Equal("Точка 1", observation.PointRaw);
        Assert.Equal("Північ", observation.LocationRaw);
        Assert.Equal("Район А", observation.DistrictRaw);
        Assert.Equal("2 мсб", observation.SubdivisionRaw);
        Assert.Equal("2 мсб", observation.SubdivisionNorm);
        Assert.Equal(SubdivisionLinkStrength.Strong, observation.SubdivisionStrength);
        Assert.Equal(ObservationSubdivisionSource.Note, observation.SubdivisionSource);
        Assert.Equal("Коротка примітка", observation.Note);
        Assert.Equal("analyst", observation.CreatedBy);
        Assert.False(string.IsNullOrWhiteSpace(observation.ContentHash));
    }

    [Fact]
    public void AddParticipant_Rejects_Duplicate_Known_Label_By_Normalized_Value()
    {
        var observation = CreateObservation();
        observation.AddParticipant("Клим", isUnknown: false, roleRaw: "водій");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            observation.AddParticipant("  клим  ", isUnknown: false, roleRaw: "стрілець"));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unknown_Participant_Hash_Does_Not_Depend_On_Label_Text()
    {
        var observedAt = new DateTime(2026, 3, 20, 9, 15, 0, DateTimeKind.Utc);

        var first = Observation.Create(observedAt, "Рух", rmRaw: "Р-111");
        first.AddParticipant("НВ 1", isUnknown: true, roleRaw: "водій", ordinal: 1);

        var second = Observation.Create(observedAt, "Рух", rmRaw: "Р-111");
        second.AddParticipant("НВ із складу 656 мсп", isUnknown: true, roleRaw: "водій", ordinal: 1);

        Assert.Equal(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void UpdateParticipant_Preserves_StartedAsUnknown_After_Resolution_As_Known()
    {
        var observation = CreateObservation();
        var participant = observation.AddParticipant("НВ 2", isUnknown: true, roleRaw: "водій");

        observation.UpdateParticipant(participant.Id, "Клим", isUnknown: false, roleRaw: "водій");

        var updated = Assert.Single(observation.Participants);
        Assert.False(updated.IsUnknown);
        Assert.True(updated.StartedAsUnknown);
        Assert.Equal("Клим", updated.LabelRaw);
        Assert.Equal("клим", updated.LabelNorm);
    }

    [Fact]
    public void BindAction_Does_Not_Overwrite_Raw_Action_Text()
    {
        var observation = CreateObservation(actionRaw: "Сирий текст дії");
        var actionId = Guid.NewGuid();

        observation.BindAction(actionId);

        Assert.Equal(actionId, observation.ObservationActionId);
        Assert.Equal("Сирий текст дії", observation.ActionRaw);
        Assert.Equal("сирий текст дії", observation.ActionNorm);

        observation.ClearBoundAction();
        Assert.Null(observation.ObservationActionId);
    }

    [Fact]
    public void UpdateContext_And_Timing_Recompute_Hash()
    {
        var observation = CreateObservation();
        var initialHash = observation.ContentHash;

        observation.UpdateContext(
            actionRaw: "Нова дія",
            layer: "Шар 2",
            rmRaw: "Р-777",
            pointRaw: "Точка 7",
            locationRaw: "Південь",
            districtRaw: "Район Б",
            note: "нова примітка");
        var afterContext = observation.ContentHash;

        observation.UpdateTiming(new DateTime(2026, 3, 20, 22, 10, 0, DateTimeKind.Utc));

        Assert.NotEqual(initialHash, afterContext);
        Assert.NotEqual(afterContext, observation.ContentHash);
        Assert.Equal("Нова дія", observation.ActionRaw);
        Assert.Equal("нова дія", observation.ActionNorm);
        Assert.Equal("Район Б", observation.DistrictRaw);
        Assert.Equal("нова примітка", observation.Note);
    }

    [Fact]
    public void UpdateSubdivision_Clears_Metadata_When_Raw_Value_Becomes_Empty()
    {
        var observation = CreateObservation();

        observation.UpdateSubdivision("  3 мсб  ", SubdivisionLinkStrength.Medium, ObservationSubdivisionSource.Import);
        Assert.Equal("3 мсб", observation.SubdivisionRaw);
        Assert.Equal(SubdivisionLinkStrength.Medium, observation.SubdivisionStrength);
        Assert.Equal(ObservationSubdivisionSource.Import, observation.SubdivisionSource);

        observation.UpdateSubdivision("   ", SubdivisionLinkStrength.Strong, ObservationSubdivisionSource.Note);

        Assert.Null(observation.SubdivisionRaw);
        Assert.Null(observation.SubdivisionNorm);
        Assert.Null(observation.SubdivisionStrength);
        Assert.Null(observation.SubdivisionSource);
    }

    [Fact]
    public void AddTag_Rejects_Duplicate_By_Kind_And_Normalized_Value()
    {
        var observation = CreateObservation();
        observation.AddTag("Клим", TagKind.Person);

        var ex = Assert.Throws<InvalidOperationException>(() => observation.AddTag("  клим  ", TagKind.Person));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateTag_Rejects_Duplicate_Target()
    {
        var observation = CreateObservation();
        var first = observation.AddTag("Клим", TagKind.Person);
        var second = observation.AddTag("Сом", TagKind.Person);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            observation.UpdateTag(second.Id, "  клим ", TagKind.Person));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);

        var unchanged = observation.Tags.Single(t => t.Id == second.Id);
        Assert.Equal("Сом", unchanged.RawValue);
    }

    [Fact]
    public void AddProbableAction_Rejects_Duplicate_For_Same_Action()
    {
        var observation = CreateObservation();
        var actionId = Guid.NewGuid();
        observation.AddProbableAction(actionId, 0.70m, "shape match", ProbableActionSource.Rule);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            observation.AddProbableAction(actionId, 0.80m, "duplicate", ProbableActionSource.Manual));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemoveParticipant_Tag_And_ProbableAction_Work_For_Existing_Items()
    {
        var observation = CreateObservation();
        var participant = observation.AddParticipant("Клим", isUnknown: false);
        var tag = observation.AddTag("район а", TagKind.Location);
        var probableAction = observation.AddProbableAction(Guid.NewGuid(), 0.5m, "maybe", ProbableActionSource.Derived);

        observation.RemoveParticipant(participant.Id);
        observation.RemoveTag(tag.Id);
        observation.RemoveProbableAction(probableAction.Id);

        Assert.Empty(observation.Participants);
        Assert.Empty(observation.Tags);
        Assert.Empty(observation.ProbableActions);
    }

    private static Observation CreateObservation(string actionRaw = "Рух")
        => Observation.Create(new DateTime(2026, 3, 20, 8, 30, 0, DateTimeKind.Utc), actionRaw, rmRaw: "Р-101");
}
