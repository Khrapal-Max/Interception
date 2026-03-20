//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Catalogs.Dtos;
using Interception.UI.Application.Catalogs.Services;
using Interception.UI.Domain;

namespace Interception.Tests.Application.Catalogs;

public sealed class ResolvedSubdivisionCatalogServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsSubdivision_AndReturnsLinkedClustersCount()
    {
        var factory = TestDbFactory.CreateFactory();
        var sut = new ResolvedSubdivisionCatalogService(factory);

        var created = await sut.CreateAsync(
            new ResolvedSubdivisionCatalogUpsertDto("  3 мсб  ", "  1/2  ", "  RM-77  ", "  підтверджений  "),
            "tester",
            CancellationToken.None);

        Assert.True(created.Created);
        Assert.NotEqual(Guid.Empty, created.Id);

        using (var db = factory.CreateDbContext())
        {
            var cluster = UnknownSubdivisionCluster.Create("невідомий 3 мсб", "1/2", "RM-77", "source", "analyst");
            cluster.Resolve(created.Id);
            db.UnknownSubdivisionClusters.Add(cluster);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var details = await sut.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(details);
        Assert.Equal("3 мсб", details!.Name);
        Assert.Equal("1/2", details.LayerHint);
        Assert.Equal("RM-77", details.RmHint);
        Assert.Equal("підтверджений", details.Note);
        Assert.True(details.IsActive);
        Assert.Equal(1, details.LinkedClustersCount);
        Assert.Equal("tester", details.CreatedBy);

        var page = await sut.SearchAsync(new ResolvedSubdivisionCatalogFilterDto(null, false, 0, 20), CancellationToken.None);
        var item = Assert.Single(page.Items);
        Assert.Equal(created.Id, item.Id);
        Assert.Equal(1, item.LinkedClustersCount);
    }

    [Fact]
    public async Task UpdateAsync_ChangesHintsAndNote_AndRejectsDuplicateNormalizedName()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid firstId;
        Guid secondId;

        using (var db = factory.CreateDbContext())
        {
            var first = ResolvedSubdivision.Create("1 мсб", "A", "RM-1", "seed", "seed");
            var second = ResolvedSubdivision.Create("2 мсб", "B", "RM-2", "old", "seed");
            firstId = first.Id;
            secondId = second.Id;

            db.ResolvedSubdivisions.AddRange(first, second);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new ResolvedSubdivisionCatalogService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAsync(
            secondId,
            new ResolvedSubdivisionCatalogUpsertDto("  1 МСБ  ", "X", "Y", "dup"),
            CancellationToken.None));

        var result = await sut.UpdateAsync(
            secondId,
            new ResolvedSubdivisionCatalogUpsertDto("  2 мсб уточнений  ", "  C  ", "  RM-9  ", "  оновлено  "),
            CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal(secondId, result.Id);

        var details = await sut.GetByIdAsync(secondId, CancellationToken.None);
        Assert.NotNull(details);
        Assert.Equal("2 мсб уточнений", details!.Name);
        Assert.Equal("C", details.LayerHint);
        Assert.Equal("RM-9", details.RmHint);
        Assert.Equal("оновлено", details.Note);
    }

    [Fact]
    public async Task ArchiveRestore_SearchHonorsIncludeArchived()
    {
        var factory = TestDbFactory.CreateFactory();
        Guid activeId;
        Guid archivedId;

        using (var db = factory.CreateDbContext())
        {
            var active = ResolvedSubdivision.Create("Активний підрозділ", "A", "RM-A");
            var archived = ResolvedSubdivision.Create("Архівний підрозділ", "B", "RM-B");
            archived.Archive();
            activeId = active.Id;
            archivedId = archived.Id;

            db.ResolvedSubdivisions.AddRange(active, archived);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sut = new ResolvedSubdivisionCatalogService(factory);

        var visible = await sut.SearchAsync(new ResolvedSubdivisionCatalogFilterDto(null, false, 0, 20), CancellationToken.None);
        Assert.Single(visible.Items);
        Assert.Equal(activeId, visible.Items[0].Id);

        var all = await sut.SearchAsync(new ResolvedSubdivisionCatalogFilterDto(null, true, 0, 20), CancellationToken.None);
        Assert.Equal(2, all.TotalCount);
        Assert.Contains(all.Items, x => x.Id == archivedId && !x.IsActive);

        await sut.RestoreAsync(archivedId, CancellationToken.None);

        var afterRestore = await sut.SearchAsync(new ResolvedSubdivisionCatalogFilterDto(null, false, 0, 20), CancellationToken.None);
        Assert.Equal(2, afterRestore.TotalCount);
        Assert.Contains(afterRestore.Items, x => x.Id == archivedId && x.IsActive);

        await sut.ArchiveAsync(activeId, CancellationToken.None);

        var finalPage = await sut.SearchAsync(new ResolvedSubdivisionCatalogFilterDto(null, false, 0, 20), CancellationToken.None);
        Assert.Single(finalPage.Items);
        Assert.Equal(archivedId, finalPage.Items[0].Id);
    }
}
