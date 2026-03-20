//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Application.Observations.Services;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;

namespace Interception.Tests.Application;

public sealed class ObservationRegistryServiceTests
{
    [Fact]
    public async Task SearchAsync_AppliesStructuredFilters_AndBuildsParticipantPreview()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid actionId;

        using (var db = factory.CreateDbContext())
        {
            var action = ObservationAction.Create("Радіообмін");
            actionId = action.Id;
            db.ObservationActions.Add(action);

            var matching = Observation.Create(
                new DateTime(2026, 3, 26, 9, 10, 0),
                "Доповідь про рух",
                layer: "L-1",
                rmRaw: "RM-1",
                locationRaw: "Посадка",
                districtRaw: "Район 1",
                subdivisionRaw: "656 мсп",
                subdivisionStrength: SubdivisionLinkStrength.Strong,
                subdivisionSource: ObservationSubdivisionSource.Manual,
                note: "match");
            matching.BindAction(actionId);
            matching.AddParticipant("Клим", false, "водій", 1);
            matching.AddParticipant(null, true, "стрілець", 2);
            matching.AddParticipant("Сом", false, "навідник", 3);
            matching.AddParticipant("Шторм", false, "командир", 4);
            matching.AddTag("Клим", TagKind.Person);
            matching.AddProbableAction(actionId, 0.85m, "reason", ProbableActionSource.Rule);

            var other = Observation.Create(
                new DateTime(2026, 3, 27, 11, 0, 0),
                "Інший запис",
                layer: "L-9",
                rmRaw: "RM-9",
                locationRaw: "Поле",
                districtRaw: "Район 9",
                subdivisionRaw: "69 обрп",
                subdivisionStrength: SubdivisionLinkStrength.Weak,
                subdivisionSource: ObservationSubdivisionSource.Import,
                note: "other");
            other.AddParticipant("Сом", false, "коригувальник", 1);
            other.AddTag("Поле", TagKind.Location);

            db.Observations.AddRange(matching, other);
            await db.SaveChangesAsync();
        }

        var sut = new ObservationRegistryService(factory);
        var filter = new ObservationRegistryFilterDto
        {
            ObservedFrom = new DateTime(2026, 3, 26, 0, 0, 0),
            ObservedTo = new DateTime(2026, 3, 26, 23, 59, 59),
            ObservationActionId = actionId,
            TagKind = TagKind.Person,
            SubdivisionStrength = SubdivisionLinkStrength.Strong,
            OnlyUnknownParticipants = true,
            OnlyBoundAction = true,
            Skip = 0,
            Take = 25
        };

        var page = await sut.SearchAsync(filter, CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal("Доповідь про рух", item.ActionRaw);
        Assert.Equal(actionId, item.ObservationActionId);
        Assert.Equal("Радіообмін", item.ObservationActionName);
        Assert.Equal("L-1", item.Layer);
        Assert.Equal("RM-1", item.RmRaw);
        Assert.Equal("Посадка", item.LocationRaw);
        Assert.Equal("Район 1", item.DistrictRaw);
        Assert.Equal("656 мсп", item.SubdivisionRaw);
        Assert.True(item.HasUnknownParticipants);
        Assert.Equal(4, item.ParticipantsCount);
        Assert.Equal(1, item.TagsCount);
        Assert.Equal(1, item.ProbableActionsCount);
        Assert.Equal(["Клим", "НВ 2", "Сом"], item.ParticipantsPreview);
    }

    private static readonly int[] expected = [1, 2];
    private static readonly string[] expectedArray = ["Перший", "Другий"];
    private static readonly string[] expectedArray0 = ["Альфа", "Бета", "Клим"];
    private static readonly decimal[] expectedArray1 = [0.90m, 0.55m];
    private static readonly string[] expectedArray2 = ["Обстріл", "Рух"];

    [Fact]
    public async Task GetByIdAsync_ReturnsParticipantsTagsAndProbableActionsInStableOrder()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid moveActionId;
        Guid fireActionId;
        Guid observationId;

        using (var db = factory.CreateDbContext())
        {
            var moveAction = ObservationAction.Create("Рух");
            var fireAction = ObservationAction.Create("Обстріл", ObservationActionCategory.Fire);
            moveActionId = moveAction.Id;
            fireActionId = fireAction.Id;

            db.ObservationActions.AddRange(moveAction, fireAction);

            var observation = Observation.Create(
                new DateTime(2026, 3, 27, 5, 45, 0),
                "Складний запис",
                layer: "L-2",
                rmRaw: "RM-2",
                pointRaw: "P-2",
                locationRaw: "Посадка",
                districtRaw: "Район 2",
                subdivisionRaw: "3 мсб",
                subdivisionStrength: SubdivisionLinkStrength.Medium,
                subdivisionSource: ObservationSubdivisionSource.Import,
                note: "деталі");
            observation.BindAction(moveActionId);
            observation.AddParticipant("Другий", false, "роль 2", 2);
            observation.AddParticipant("Перший", false, "роль 1", 1);
            observation.AddTag("Бета", TagKind.Keyword);
            observation.AddTag("Альфа", TagKind.Keyword);
            observation.AddTag("Клим", TagKind.Person);
            observation.AddProbableAction(moveActionId, 0.55m, "mid", ProbableActionSource.Manual);
            observation.AddProbableAction(fireActionId, 0.90m, "high", ProbableActionSource.Rule);

            db.Observations.Add(observation);
            await db.SaveChangesAsync();
            observationId = observation.Id;
        }

        var sut = new ObservationRegistryService(factory);
        var details = await sut.GetByIdAsync(observationId, CancellationToken.None);

        Assert.NotNull(details);
        Assert.Equal(observationId, details!.Id);
        Assert.Equal("Складний запис", details.ActionRaw);
        Assert.Equal(moveActionId, details.ObservationActionId);
        Assert.Equal("Рух", details.ObservationActionName);
        Assert.Equal("L-2", details.Layer);
        Assert.Equal("RM-2", details.RmRaw);
        Assert.Equal("P-2", details.PointRaw);
        Assert.Equal("Посадка", details.LocationRaw);
        Assert.Equal("Район 2", details.DistrictRaw);
        Assert.Equal("3 мсб", details.SubdivisionRaw);
        Assert.Equal(SubdivisionLinkStrength.Medium, details.SubdivisionStrength);
        Assert.Equal(ObservationSubdivisionSource.Import, details.SubdivisionSource);
        Assert.Equal("деталі", details.Note);

        Assert.Equal(expected, details.Participants.Select(x => x.Ordinal).ToArray());
        Assert.Equal(expectedArray, details.Participants.Select(x => x.LabelRaw).ToArray());
        Assert.Equal(expectedArray0, details.Tags.Select(x => x.RawValue).ToArray());
        Assert.Equal(expectedArray1, details.ProbableActions.Select(x => x.Confidence).ToArray());
        Assert.Equal(expectedArray2, details.ProbableActions.Select(x => x.ObservationActionName).ToArray());
    }
}
