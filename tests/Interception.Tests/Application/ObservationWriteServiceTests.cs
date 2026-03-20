//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Application.Observations.Services;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Interception.Tests.Application;

public sealed class ObservationWriteServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsNormalizedAggregateAndChildren()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid actionId;
        Guid tagCatalogId;

        using (var db = factory.CreateDbContext())
        {
            var action = ObservationAction.Create("Радіообмін");
            var catalog = TagCatalog.Create("Клим", TagKind.Person);
            actionId = action.Id;
            tagCatalogId = catalog.Id;

            db.ObservationActions.Add(action);
            db.TagCatalogs.Add(catalog);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new ObservationWriteService(factory);
        var observedDate = new DateTime(2026, 3, 20, 14, 35, 0);

        var request = new ObservationUpsertRequestDto
        {
            ObservedDate = observedDate,
            ObservationActionId = actionId,
            ActionRaw = "  Доповідь про рух  ",
            Layer = "  1/2  ",
            RmRaw = "  Р-149  ",
            PointRaw = "  ПС-1  ",
            LocationRaw = "  посадка  ",
            DistrictRaw = "  район північ  ",
            SubdivisionRaw = "  3 мсб  ",
            SubdivisionStrength = SubdivisionLinkStrength.Strong,
            SubdivisionSource = ObservationSubdivisionSource.Manual,
            Note = "  важлива примітка  ",
            Participants =
            [
                new ObservationParticipantUpsertDto { LabelRaw = "  Клим  ", RoleRaw = "  водій  ", Ordinal = 1 },
                new ObservationParticipantUpsertDto { LabelRaw = "  НВ 7  ", RoleRaw = "  навідник  ", Ordinal = 2 }
            ],
            Tags =
            [
                new ObservationTagUpsertDto { RawValue = "  Клим  ", Kind = TagKind.Person, TagCatalogId = tagCatalogId },
                new ObservationTagUpsertDto { RawValue = "   ", Kind = TagKind.Keyword }
            ],
            ProbableActions =
            [
                new ObservationProbableActionUpsertDto { ObservationActionId = actionId, Confidence = 0.85m, Reason = "  збіг по фразі  ", Source = ProbableActionSource.Rule },
                new ObservationProbableActionUpsertDto { ObservationActionId = Guid.Empty, Confidence = 0.55m, Reason = "ignored" }
            ]
        };

        var result = await sut.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.NotEqual(Guid.Empty, result.ObservationId);
        Assert.False(string.IsNullOrWhiteSpace(result.ContentHash));

        using var verifyDb = factory.CreateDbContext();
        var saved = await verifyDb.Observations
            .Include(x => x.Participants)
            .Include(x => x.Tags)
            .Include(x => x.ProbableActions)
            .SingleAsync(x => x.Id == result.ObservationId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(observedDate, saved.ObservedDate);
        Assert.Equal(actionId, saved.ObservationActionId);
        Assert.Equal("Доповідь про рух", saved.ActionRaw);
        Assert.Equal("1/2", saved.Layer);
        Assert.Equal("Р-149", saved.RmRaw);
        Assert.Equal("ПС-1", saved.PointRaw);
        Assert.Equal("посадка", saved.LocationRaw);
        Assert.Equal("район північ", saved.DistrictRaw);
        Assert.Equal("3 мсб", saved.SubdivisionRaw);
        Assert.Equal(SubdivisionLinkStrength.Strong, saved.SubdivisionStrength);
        Assert.Equal(ObservationSubdivisionSource.Manual, saved.SubdivisionSource);
        Assert.Equal("важлива примітка", saved.Note);

        Assert.Collection(
            saved.Participants.OrderBy(x => x.Ordinal),
            known =>
            {
                Assert.Equal("Клим", known.LabelRaw);
                Assert.False(known.IsUnknown);
                Assert.False(known.StartedAsUnknown);
                Assert.Equal("водій", known.RoleRaw);
                Assert.Equal(1, known.Ordinal);
            },
            unknown =>
            {
                Assert.Equal("НВ 7", unknown.LabelRaw);
                Assert.True(unknown.IsUnknown);
                Assert.True(unknown.StartedAsUnknown);
                Assert.Equal("навідник", unknown.RoleRaw);
                Assert.Equal(2, unknown.Ordinal);
            });

        var tag = Assert.Single(saved.Tags);
        Assert.Equal("Клим", tag.RawValue);
        Assert.Equal(TagKind.Person, tag.Kind);
        Assert.Equal(tagCatalogId, tag.TagCatalogId);

        var probableAction = Assert.Single(saved.ProbableActions);
        Assert.Equal(actionId, probableAction.ObservationActionId);
        Assert.Equal(0.85m, probableAction.Confidence);
        Assert.Equal("збіг по фразі", probableAction.Reason);
        Assert.Equal(ProbableActionSource.Rule, probableAction.Source);
    }

    [Fact]
    public async Task CreateAsync_ReturnsDuplicate_WhenUnknownLabelsDifferButContentHashMatches()
    {
        var factory = TestDbFactory.CreateFactory();
        var sut = new ObservationWriteService(factory);
        var observedDate = new DateTime(2026, 3, 21, 8, 10, 0);

        var firstRequest = new ObservationUpsertRequestDto
        {
            ObservedDate = observedDate,
            ActionRaw = "Доповідь",
            Layer = "A-1",
            Participants =
            [
                new ObservationParticipantUpsertDto { LabelRaw = "НВ 1 мсб", IsUnknown = true, RoleRaw = "водій", Ordinal = 1 }
            ]
        };

        var secondRequest = new ObservationUpsertRequestDto
        {
            ObservedDate = observedDate,
            ActionRaw = "Доповідь",
            Layer = "A-1",
            Participants =
            [
                new ObservationParticipantUpsertDto { LabelRaw = "НВ 9 інший ярлик", IsUnknown = true, RoleRaw = "водій", Ordinal = 1 }
            ]
        };

        var first = await sut.CreateAsync(firstRequest, CancellationToken.None);
        var second = await sut.CreateAsync(secondRequest, CancellationToken.None);

        Assert.False(first.IsDuplicate);
        Assert.True(second.IsDuplicate);
        Assert.Equal(first.ObservationId, second.ObservationId);
        Assert.Equal(first.ContentHash, second.ContentHash);

        using var db = factory.CreateDbContext();
        Assert.Equal(1, await db.Observations.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_ReplacesParticipantsTagsAndProbableActions()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid actionMoveId;
        Guid actionFireId;
        Guid tagCatalogId;

        using (var db = factory.CreateDbContext())
        {
            var moveAction = ObservationAction.Create("Рух");
            var fireAction = ObservationAction.Create("Обстріл", ObservationActionCategory.Fire);
            var tagCatalog = TagCatalog.Create("Ліс", TagKind.Location);

            actionMoveId = moveAction.Id;
            actionFireId = fireAction.Id;
            tagCatalogId = tagCatalog.Id;

            db.ObservationActions.AddRange(moveAction, fireAction);
            db.TagCatalogs.Add(tagCatalog);

            var observation = Observation.Create(
                new DateTime(2026, 3, 22, 6, 0, 0),
                "Старий рух",
                layer: "L-1",
                rmRaw: "RM-1",
                locationRaw: "Посадка",
                districtRaw: "Район 1",
                subdivisionRaw: "1 мсб",
                subdivisionStrength: SubdivisionLinkStrength.Weak,
                subdivisionSource: ObservationSubdivisionSource.Import,
                note: "старий note");

            observation.BindAction(actionMoveId);
            observation.AddParticipant("Клим", false, "водій", 1);
            observation.AddParticipant("НВ 1", true, "стрілець", 2);
            observation.AddTag("Ліс", TagKind.Location, ObservationTagSource.Manual, tagCatalogId);
            observation.AddTag("Старий", TagKind.Keyword);
            observation.AddProbableAction(actionMoveId, 0.40m, "старий reason", ProbableActionSource.Manual);

            db.Observations.Add(observation);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Guid observationId;
        Guid knownParticipantId;
        Guid locationTagId;
        Guid probableActionId;

        using (var db = factory.CreateDbContext())
        {
            var saved = await db.Observations
                .Include(x => x.Participants)
                .Include(x => x.Tags)
                .Include(x => x.ProbableActions)
                .SingleAsync(cancellationToken: TestContext.Current.CancellationToken);

            observationId = saved.Id;
            knownParticipantId = saved.Participants.Single(x => !x.IsUnknown).Id;
            locationTagId = saved.Tags.Single(x => x.Kind == TagKind.Location).Id;
            probableActionId = saved.ProbableActions.Single().Id;
        }

        var sut = new ObservationWriteService(factory);
        var request = new ObservationUpsertRequestDto
        {
            ObservedDate = new DateTime(2026, 3, 22, 7, 15, 0),
            ObservationActionId = actionFireId,
            ActionRaw = "  Новий рух і обстріл  ",
            Layer = "  L-2  ",
            RmRaw = "  RM-2  ",
            PointRaw = "  P-2  ",
            LocationRaw = "  Галявина  ",
            DistrictRaw = "  Район 2  ",
            SubdivisionRaw = "  2 мсб  ",
            SubdivisionStrength = SubdivisionLinkStrength.Strong,
            SubdivisionSource = ObservationSubdivisionSource.Manual,
            Note = "  новий note  ",
            Participants =
            [
                new ObservationParticipantUpsertDto { Id = knownParticipantId, LabelRaw = "Клим", IsUnknown = false, RoleRaw = "старший водій", Ordinal = 1 },
                new ObservationParticipantUpsertDto { LabelRaw = "НВ із складу 656 мсп", IsUnknown = true, RoleRaw = "коригувальник", Ordinal = 3 }
            ],
            Tags =
            [
                new ObservationTagUpsertDto { Id = locationTagId, RawValue = "  Галявина  ", Kind = TagKind.Location, TagCatalogId = tagCatalogId },
                new ObservationTagUpsertDto { RawValue = "  новий ключ  ", Kind = TagKind.Keyword }
            ],
            ProbableActions =
            [
                new ObservationProbableActionUpsertDto { Id = probableActionId, ObservationActionId = actionMoveId, Confidence = 0.75m, Reason = "  уточнено  ", Source = ProbableActionSource.Rule },
                new ObservationProbableActionUpsertDto { ObservationActionId = actionFireId, Confidence = 0.95m, Reason = "  майже точно  ", Source = ProbableActionSource.Manual }
            ]
        };

        var result = await sut.UpdateAsync(observationId, request, CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.Equal(observationId, result.ObservationId);

        using var verifyDb = factory.CreateDbContext();
        var updated = await verifyDb.Observations
            .Include(x => x.Participants)
            .Include(x => x.Tags)
            .Include(x => x.ProbableActions)
            .SingleAsync(x => x.Id == observationId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new DateTime(2026, 3, 22, 7, 15, 0), updated.ObservedDate);
        Assert.Equal(actionFireId, updated.ObservationActionId);
        Assert.Equal("Новий рух і обстріл", updated.ActionRaw);
        Assert.Equal("L-2", updated.Layer);
        Assert.Equal("RM-2", updated.RmRaw);
        Assert.Equal("P-2", updated.PointRaw);
        Assert.Equal("Галявина", updated.LocationRaw);
        Assert.Equal("Район 2", updated.DistrictRaw);
        Assert.Equal("2 мсб", updated.SubdivisionRaw);
        Assert.Equal(SubdivisionLinkStrength.Strong, updated.SubdivisionStrength);
        Assert.Equal(ObservationSubdivisionSource.Manual, updated.SubdivisionSource);
        Assert.Equal("новий note", updated.Note);

        Assert.Collection(
            updated.Participants.OrderBy(x => x.Ordinal),
            known =>
            {
                Assert.Equal(knownParticipantId, known.Id);
                Assert.Equal("Клим", known.LabelRaw);
                Assert.Equal("старший водій", known.RoleRaw);
                Assert.False(known.IsUnknown);
            },
            unknown =>
            {
                Assert.Equal("НВ із складу 656 мсп", unknown.LabelRaw);
                Assert.True(unknown.IsUnknown);
                Assert.True(unknown.StartedAsUnknown);
                Assert.Equal(3, unknown.Ordinal);
            });

        Assert.Collection(
            updated.Tags.OrderBy(x => x.Kind).ThenBy(x => x.RawValue),
            first => Assert.Equal("новий ключ", first.RawValue),
            second => Assert.Equal("Галявина", second.RawValue));

        Assert.Equal(2, updated.ProbableActions.Count);
        Assert.Contains(updated.ProbableActions, x => x.Id == probableActionId && x.Confidence == 0.75m && x.Reason == "уточнено");
        Assert.Contains(updated.ProbableActions, x => x.ObservationActionId == actionFireId && x.Confidence == 0.95m && x.Reason == "майже точно");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsDuplicate_WhenAnotherObservationHasSameContentHash()
    {
        var factory = TestDbFactory.CreateFactory();
        var sut = new ObservationWriteService(factory);

        var canonical = new ObservationUpsertRequestDto
        {
            ObservedDate = new DateTime(2026, 3, 23, 9, 0, 0),
            ActionRaw = "Доповідь",
            Layer = "A-1",
            Participants =
            [
                new ObservationParticipantUpsertDto { LabelRaw = "Клим", RoleRaw = "водій", Ordinal = 1 },
                new ObservationParticipantUpsertDto { LabelRaw = "НВ 1", IsUnknown = true, RoleRaw = "стрілець", Ordinal = 2 }
            ]
        };

        var other = new ObservationUpsertRequestDto
        {
            ObservedDate = new DateTime(2026, 3, 24, 10, 0, 0),
            ActionRaw = "Інший запис",
            Layer = "B-2",
            Participants =
            [
                new ObservationParticipantUpsertDto { LabelRaw = "Сом", RoleRaw = "коригувальник", Ordinal = 1 }
            ]
        };

        var first = await sut.CreateAsync(canonical, CancellationToken.None);
        var second = await sut.CreateAsync(other, CancellationToken.None);

        var duplicate = await sut.UpdateAsync(second.ObservationId, canonical, CancellationToken.None);

        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(first.ObservationId, duplicate.ObservationId);

        using var db = factory.CreateDbContext();
        var savedSecond = await db.Observations.SingleAsync(x => x.Id == second.ObservationId, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Інший запис", savedSecond.ActionRaw);
        Assert.Equal("B-2", savedSecond.Layer);
    }

}
