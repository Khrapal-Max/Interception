//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;
using Interception.UI.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Interception.Tests.Infrastructure;

public sealed class AppDbContextModelTests
{
    [Fact]
    public void Model_Contains_Only_Current_Observation_Entities_For_This_Branch()
    {
        using var context = CreateContext();
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(Observation)));
        Assert.NotNull(model.FindEntityType(typeof(ObservationParticipant)));
        Assert.NotNull(model.FindEntityType(typeof(ObservationAction)));
    }

    [Fact]
    public void Observation_Model_Has_Expected_Table_Columns_Index_And_ForeignKey()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(Observation));
        Assert.NotNull(entity);

        Assert.Equal("link_observations", entity!.GetTableName());

        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        Assert.Equal("observed_date", entity.FindProperty(nameof(Observation.ObservedDate))!.GetColumnName(table));
        Assert.Equal("observation_action_id", entity.FindProperty(nameof(Observation.ObservationActionId))!.GetColumnName(table));
        Assert.Equal("content_hash", entity.FindProperty(nameof(Observation.ContentHash))!.GetColumnName(table));

        var contentHashIndex = entity.GetIndexes().Single(x =>
            x.Properties.Select(p => p.Name).SequenceEqual([nameof(Observation.ContentHash)]));
        Assert.True(contentHashIndex.IsUnique);

        var fk = entity.GetForeignKeys().Single(x => x.PrincipalEntityType.ClrType == typeof(ObservationAction));
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Fact]
    public void ObservationParticipant_Model_Has_StartedAsUnknown_And_KnownOnly_Unique_Filter()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ObservationParticipant));
        Assert.NotNull(entity);

        Assert.Equal("link_observation_participants", entity!.GetTableName());

        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        Assert.Equal("started_as_unknown", entity.FindProperty(nameof(ObservationParticipant.StartedAsUnknown))!.GetColumnName(table));
        Assert.Equal("ordinal", entity.FindProperty(nameof(ObservationParticipant.Ordinal))!.GetColumnName(table));

        var ordinalIndex = entity.GetIndexes().Single(x =>
            x.Properties.Select(p => p.Name).SequenceEqual([nameof(ObservationParticipant.ObservationId), nameof(ObservationParticipant.Ordinal)]));
        Assert.True(ordinalIndex.IsUnique);

        var knownLabelIndex = entity.GetIndexes().Single(x =>
            x.Properties.Select(p => p.Name).SequenceEqual([nameof(ObservationParticipant.ObservationId), nameof(ObservationParticipant.LabelNorm)]));

        Assert.True(knownLabelIndex.IsUnique);
        Assert.Equal("is_unknown = false and label_norm is not null", knownLabelIndex.GetFilter());
    }

    [Fact]
    public void ObservationAction_Model_Has_Table_And_Expected_Indexes()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ObservationAction));
        Assert.NotNull(entity);

        Assert.Equal("link_observation_actions", entity!.GetTableName());

        var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        Assert.Equal("name_norm", entity.FindProperty(nameof(ObservationAction.NameNorm))!.GetColumnName(table));
        Assert.Equal("is_active", entity.FindProperty(nameof(ObservationAction.IsActive))!.GetColumnName(table));

        var uniqueNameNorm = entity.GetIndexes().Single(x =>
            x.Properties.Select(p => p.Name).SequenceEqual([nameof(ObservationAction.NameNorm)]));
        Assert.True(uniqueNameNorm.IsUnique);

        var activeNameNorm = entity.GetIndexes().Single(x =>
            x.Properties.Select(p => p.Name).SequenceEqual([nameof(ObservationAction.IsActive), nameof(ObservationAction.NameNorm)]));
        Assert.False(activeNameNorm.IsUnique);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}
