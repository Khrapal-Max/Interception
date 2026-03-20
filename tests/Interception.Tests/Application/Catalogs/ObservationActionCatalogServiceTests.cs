//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Tests;
using Interception.UI.Application.Catalogs.Dtos;
using Interception.UI.Application.Catalogs.Services;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Interception.UI.Tests.Application.Catalogs;

public sealed class ObservationActionCatalogServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsTypedAction_AndReturnsDetailsAndCounts()
    {
        var factory = TestDbFactory.CreateFactory();
        var sut = new ObservationActionCatalogService(factory);

        var created = await sut.CreateAsync(
            new ObservationActionCatalogUpsertDto(
                "  Радіообмін  ",
                ObservationActionCategory.Communication,
                "  оператор  ",
                "  адресат  ",
                "  короткий опис  ",
                2,
                true),
            "tester",
            CancellationToken.None);

        Assert.True(created.Created);
        Assert.NotEqual(Guid.Empty, created.Id);

        Guid createdId = created.Id;

        using (var db = factory.CreateDbContext())
        {
            var action = await db.ObservationActions.SingleAsync(x => x.Id == createdId, cancellationToken: TestContext.Current.CancellationToken);

            var observation = Observation.Create(
                new DateTime(2026, 3, 20, 9, 15, 0),
                "Доповідь",
                layer: "L1",
                note: "note");

            observation.BindAction(action.Id);
            observation.AddProbableAction(action.Id, 0.81m, "rule", ProbableActionSource.Rule);

            db.Observations.Add(observation);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var details = await sut.GetByIdAsync(createdId, CancellationToken.None);
        Assert.NotNull(details);
        Assert.Equal("Радіообмін", details!.Name);
        Assert.Equal(ObservationActionCategory.Communication, details.Category);
        Assert.Equal("оператор", details.InitiatorRoleName);
        Assert.Equal("адресат", details.ResponderRoleName);
        Assert.Equal("короткий опис", details.Description);
        Assert.Equal((short)2, details.TypicalParticipantsCount);
        Assert.True(details.RequiresCounterparty);
        Assert.True(details.IsActive);
        Assert.Equal(1, details.BoundObservationsCount);
        Assert.Equal(1, details.ProbableUsageCount);
        Assert.Equal("tester", details.CreatedBy);

        var page = await sut.SearchAsync(
            new ObservationActionCatalogFilterDto(null, ObservationActionCategory.Communication, false, 0, 20),
            CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal(createdId, item.Id);
        Assert.Equal(1, item.BoundObservationsCount);
        Assert.Equal(1, item.ProbableUsageCount);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields_AndRejectsDuplicateNormalizedName()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid firstId;
        Guid secondId;

        using (var db = factory.CreateDbContext())
        {
            var first = ObservationAction.Create("Рух", ObservationActionCategory.Movement, "ініц", null, null, 1, false, "seed");
            var second = ObservationAction.Create("Обстріл", ObservationActionCategory.Fire, "старий", "ціль", "desc", 3, true, "seed");
            firstId = first.Id;
            secondId = second.Id;

            db.ObservationActions.AddRange(first, second);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new ObservationActionCatalogService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAsync(
            secondId,
            new ObservationActionCatalogUpsertDto("  рух  ", ObservationActionCategory.Fire, null, null, null, 1, false),
            CancellationToken.None));

        var result = await sut.UpdateAsync(
            secondId,
            new ObservationActionCatalogUpsertDto("  Контрбатарейний вогонь  ", ObservationActionCategory.Fire, "  батарея  ", "  ціль  ", "  оновлено  ", 4, true),
            CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal(secondId, result.Id);

        var details = await sut.GetByIdAsync(secondId, CancellationToken.None);
        Assert.NotNull(details);
        Assert.Equal("Контрбатарейний вогонь", details!.Name);
        Assert.Equal("батарея", details.InitiatorRoleName);
        Assert.Equal("ціль", details.ResponderRoleName);
        Assert.Equal("оновлено", details.Description);
        Assert.Equal((short)4, details.TypicalParticipantsCount);
        Assert.True(details.RequiresCounterparty);
    }

    [Fact]
    public async Task ArchiveRestore_SearchHonorsIncludeArchived()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid activeId;
        Guid archivedId;

        using (var db = factory.CreateDbContext())
        {
            var active = ObservationAction.Create("Розвідка", ObservationActionCategory.Recon);
            var archived = ObservationAction.Create("Резервна дія", ObservationActionCategory.Other);
            archived.Archive();
            activeId = active.Id;
            archivedId = archived.Id;

            db.ObservationActions.AddRange(active, archived);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new ObservationActionCatalogService(factory);

        var visible = await sut.SearchAsync(
            new ObservationActionCatalogFilterDto(null, null, false, 0, 20),
            CancellationToken.None);

        Assert.Single(visible.Items);
        Assert.Equal(activeId, visible.Items[0].Id);

        var all = await sut.SearchAsync(
            new ObservationActionCatalogFilterDto(null, null, true, 0, 20),
            CancellationToken.None);

        Assert.Equal(2, all.TotalCount);
        Assert.Contains(all.Items, x => x.Id == archivedId && !x.IsActive);

        await sut.RestoreAsync(archivedId, CancellationToken.None);

        var afterRestore = await sut.SearchAsync(
            new ObservationActionCatalogFilterDto(null, null, false, 0, 20),
            CancellationToken.None);

        Assert.Equal(2, afterRestore.TotalCount);
        Assert.Contains(afterRestore.Items, x => x.Id == archivedId && x.IsActive);

        await sut.ArchiveAsync(activeId, CancellationToken.None);

        var finalPage = await sut.SearchAsync(
            new ObservationActionCatalogFilterDto(null, null, false, 0, 20),
            CancellationToken.None);

        Assert.Single(finalPage.Items);
        Assert.Equal(archivedId, finalPage.Items[0].Id);
    }
}
