//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;

namespace Interception.Tests.Domain;

public sealed class SubdivisionResolutionTests
{
    [Fact]
    public void ResolvedSubdivision_Create_And_Update_Work()
    {
        var subdivision = ResolvedSubdivision.Create(
            "  1 мсб  ",
            layerHint: "  шар а  ",
            rmHint: "  р-123  ",
            note: "  основна примітка  ",
            createdBy: "  analyst  ");

        subdivision.Rename("  2 мсб  ");
        subdivision.UpdateHints("  шар б  ", "  р-555  ");
        subdivision.SetNote("  оновлено  ");
        subdivision.Archive();
        subdivision.Restore();

        Assert.Equal("2 мсб", subdivision.Name);
        Assert.Equal("2 мсб", subdivision.NameNorm);
        Assert.Equal("шар б", subdivision.LayerHint);
        Assert.Equal("р-555", subdivision.RmHint);
        Assert.Equal("оновлено", subdivision.Note);
        Assert.True(subdivision.IsActive);
    }

    [Fact]
    public void UnknownSubdivisionCluster_Supports_Hints_Observations_And_Resolution()
    {
        var cluster = UnknownSubdivisionCluster.Create(
            "  656 мсп  ",
            layerHint: "  шар а  ",
            rmHint: "  р-123  ",
            note: "  первинно  ",
            createdBy: "  analyst  ");

        cluster.UpdateHints("  шар б  ", "  р-555  ");
        cluster.SetNote("  уточнено  ");

        var observationId = Guid.NewGuid();
        var link = cluster.AddObservation(observationId, note: "  observation note  ");
        link.SetNote("  змінено  ");

        cluster.Resolve(Guid.NewGuid());
        Assert.NotNull(cluster.ResolvedSubdivisionId);
        Assert.NotNull(cluster.ArchivedAtUtc);

        cluster.Reopen();
        cluster.Archive();

        Assert.Equal("656 мсп", cluster.LabelRaw);
        Assert.Equal("656 мсп", cluster.LabelNorm);
        Assert.Equal("шар б", cluster.LayerHint);
        Assert.Equal("р-555", cluster.RmHint);
        Assert.Equal("уточнено", cluster.Note);
        Assert.Single(cluster.Observations);
        Assert.Equal(observationId, link.ObservationId);
        Assert.Equal("змінено", link.Note);
        Assert.NotNull(cluster.ArchivedAtUtc);
    }

    [Fact]
    public void UnknownSubdivisionCluster_Rejects_Duplicate_Observation_Link()
    {
        var cluster = UnknownSubdivisionCluster.Create("656 мсп");
        var observationId = Guid.NewGuid();
        cluster.AddObservation(observationId);

        var ex = Assert.Throws<InvalidOperationException>(() => cluster.AddObservation(observationId));

        Assert.Contains("already belongs", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
