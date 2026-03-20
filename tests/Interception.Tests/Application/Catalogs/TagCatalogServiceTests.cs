//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Catalogs.Dtos;
using Interception.UI.Application.Catalogs.Services;
using Interception.UI.Domain;
using Interception.UI.Domain.Enums;

namespace Interception.Tests.Application.Catalogs;

public sealed class TagCatalogServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsTag_AndReturnsDetailsWithUsageCount()
    {
        var factory = TestDbFactory.CreateFactory();
        var sut = new TagCatalogService(factory);

        var created = await sut.CreateAsync(
            new TagCatalogUpsertDto("  Клим  ", TagKind.Person),
            "tester",
            CancellationToken.None);

        Assert.True(created.Created);
        Assert.NotEqual(Guid.Empty, created.Id);

        using (var db = factory.CreateDbContext())
        {
            var observation = Observation.Create(new DateTime(2026, 3, 20, 10, 0, 0), "Доповідь", layer: "L");
            observation.AddTag("Клим", TagKind.Person, ObservationTagSource.Manual, created.Id);
            db.Observations.Add(observation);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var details = await sut.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(details);
        Assert.Equal("Клим", details!.Name);
        Assert.Equal(TagKind.Person, details.Kind);
        Assert.True(details.IsActive);
        Assert.Equal(1, details.UsageCount);
        Assert.Equal("tester", details.CreatedBy);

        var page = await sut.SearchAsync(new TagCatalogFilterDto(null, TagKind.Person, false, 0, 20), CancellationToken.None);
        var item = Assert.Single(page.Items);
        Assert.Equal(created.Id, item.Id);
        Assert.Equal(1, item.UsageCount);
    }

    [Fact]
    public async Task UpdateAsync_ChangesNameAndKind_AndRejectsDuplicateForSameKind()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid firstId;
        Guid secondId;

        using (var db = factory.CreateDbContext())
        {
            var first = TagCatalog.Create("Клим", TagKind.Person, "seed");
            var second = TagCatalog.Create("Ліс", TagKind.Location, "seed");
            firstId = first.Id;
            secondId = second.Id;

            db.TagCatalogs.AddRange(first, second);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new TagCatalogService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAsync(
            secondId,
            new TagCatalogUpsertDto("  кЛиМ  ", TagKind.Person),
            CancellationToken.None));

        var result = await sut.UpdateAsync(secondId, new TagCatalogUpsertDto("  Район  ", TagKind.Keyword), CancellationToken.None);
        Assert.False(result.Created);

        var details = await sut.GetByIdAsync(secondId, CancellationToken.None);
        Assert.NotNull(details);
        Assert.Equal("Район", details!.Name);
        Assert.Equal(TagKind.Keyword, details.Kind);
    }

    [Fact]
    public async Task ArchiveRestore_SearchHonorsIncludeArchived()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid activeId;
        Guid archivedId;

        using (var db = factory.CreateDbContext())
        {
            var active = TagCatalog.Create("Активний", TagKind.Keyword);
            var archived = TagCatalog.Create("Архівний", TagKind.Location);
            archived.Archive();
            activeId = active.Id;
            archivedId = archived.Id;

            db.TagCatalogs.AddRange(active, archived);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new TagCatalogService(factory);

        var visible = await sut.SearchAsync(new TagCatalogFilterDto(null, null, false, 0, 20), CancellationToken.None);
        Assert.Single(visible.Items);
        Assert.Equal(activeId, visible.Items[0].Id);

        var all = await sut.SearchAsync(new TagCatalogFilterDto(null, null, true, 0, 20), CancellationToken.None);
        Assert.Equal(2, all.TotalCount);
        Assert.Contains(all.Items, x => x.Id == archivedId && !x.IsActive);

        await sut.RestoreAsync(archivedId, CancellationToken.None);

        var afterRestore = await sut.SearchAsync(new TagCatalogFilterDto(null, null, false, 0, 20), CancellationToken.None);
        Assert.Equal(2, afterRestore.TotalCount);
        Assert.Contains(afterRestore.Items, x => x.Id == archivedId && x.IsActive);

        await sut.ArchiveAsync(activeId, CancellationToken.None);

        var finalPage = await sut.SearchAsync(new TagCatalogFilterDto(null, null, false, 0, 20), CancellationToken.None);
        Assert.Single(finalPage.Items);
        Assert.Equal(archivedId, finalPage.Items[0].Id);
    }
}
