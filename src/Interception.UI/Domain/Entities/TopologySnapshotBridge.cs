//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain.Entities;

/// <summary>
/// Міст між двома snapshot-групами.
/// </summary>
public sealed class TopologySnapshotBridge
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid GroupId { get; private set; }
    public TopologySnapshotGroup? Group { get; private set; }

    public string TargetGroupKey { get; private set; } = string.Empty;
    public string? TargetDivision { get; private set; }
    public string ContactPersonName { get; private set; } = string.Empty;
    public string BridgeFrequency { get; private set; } = string.Empty;
    public int Weight { get; private set; }
    public string? PrimaryAction { get; private set; }
    public int SortOrder { get; private set; }

    public List<TopologySnapshotBridgeAction> Actions { get; private set; } = [];

    public static TopologySnapshotBridge Create(
        Guid groupId,
        string targetGroupKey,
        string? targetDivision,
        string contactPersonName,
        string bridgeFrequency,
        int weight,
        string? primaryAction,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(targetGroupKey))
            throw new ArgumentException("TargetGroupKey обов'язковий.", nameof(targetGroupKey));
        if (string.IsNullOrWhiteSpace(contactPersonName))
            throw new ArgumentException("ContactPersonName обов'язковий.", nameof(contactPersonName));
        if (string.IsNullOrWhiteSpace(bridgeFrequency))
            throw new ArgumentException("BridgeFrequency обов'язкова.", nameof(bridgeFrequency));

        return new TopologySnapshotBridge
        {
            GroupId = groupId,
            TargetGroupKey = targetGroupKey.Trim(),
            TargetDivision = SemanticValueExtensions.NormalizeMeaningfulOrNull(targetDivision),
            ContactPersonName = contactPersonName.Trim(),
            BridgeFrequency = bridgeFrequency.Trim(),
            Weight = weight,
            PrimaryAction = SemanticValueExtensions.NormalizeMeaningfulOrNull(primaryAction),
            SortOrder = sortOrder
        };
    }

    public void AddAction(string name, bool isPrimary, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        Actions.Add(TopologySnapshotBridgeAction.Create(Id, name, isPrimary, sortOrder));
    }
}
