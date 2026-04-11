//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Topology;

/// <summary>
/// Дія мосту snapshot-груп.
/// </summary>
public sealed class TopologySnapshotBridgeAction
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid BridgeId { get; private set; }
    public TopologySnapshotBridge? Bridge { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }
    public int SortOrder { get; private set; }

    public static TopologySnapshotBridgeAction Create(Guid bridgeId, string name, bool isPrimary, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name обов'язкове.", nameof(name));

        return new TopologySnapshotBridgeAction
        {
            BridgeId = bridgeId,
            Name = name.Trim(),
            IsPrimary = isPrimary,
            SortOrder = sortOrder
        };
    }
}
