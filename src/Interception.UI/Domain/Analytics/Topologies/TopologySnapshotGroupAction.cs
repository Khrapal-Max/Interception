//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Analytics.Topologies;

/// <summary>
/// Дія snapshot-групи.
/// </summary>
public sealed class TopologySnapshotGroupAction
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid GroupId { get; private set; }
    public TopologySnapshotGroup? Group { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public int SortOrder { get; private set; }

    public static TopologySnapshotGroupAction Create(Guid groupId, string name, bool isPrimary, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name обов'язкове.", nameof(name));

        return new TopologySnapshotGroupAction
        {
            GroupId = groupId,
            Name = name.Trim(),
            IsPrimary = isPrimary,
            SortOrder = sortOrder
        };
    }
}
