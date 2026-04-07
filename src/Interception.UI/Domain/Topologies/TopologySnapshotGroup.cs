//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain.Topology;

/// <summary>
/// Snapshot комунікаційної групи карти зв'язків.
/// </summary>
public sealed class TopologySnapshotGroup
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RunId { get; private set; }
    public TopologySnapshotRun? Run { get; private set; }

    public string GroupKey { get; private set; } = string.Empty;
    public string? Division { get; private set; }
    public string KeyPersonName { get; private set; } = string.Empty;
    public string? KeyPersonRole { get; private set; }
    public int MentionCount { get; private set; }
    public int InternalConnectionWeight { get; private set; }
    public int BridgeWeight { get; private set; }
    public int SortOrder { get; private set; }

    public List<TopologySnapshotGroupFrequency> Frequencies { get; private set; } = [];
    public List<TopologySnapshotGroupMember> Members { get; private set; } = [];
    public List<TopologySnapshotGroupAction> Actions { get; private set; } = [];
    public List<TopologySnapshotBridge> Bridges { get; private set; } = [];

    public static TopologySnapshotGroup Create(
        Guid runId,
        string groupKey,
        string? division,
        string keyPersonName,
        string? keyPersonRole,
        int mentionCount,
        int internalConnectionWeight,
        int bridgeWeight,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(groupKey))
            throw new ArgumentException("GroupKey обов'язковий.", nameof(groupKey));
        if (string.IsNullOrWhiteSpace(keyPersonName))
            throw new ArgumentException("KeyPersonName обов'язковий.", nameof(keyPersonName));

        return new TopologySnapshotGroup
        {
            RunId = runId,
            GroupKey = groupKey.Trim(),
            Division = SemanticValueExtensions.NormalizeMeaningfulOrNull(division),
            KeyPersonName = keyPersonName.Trim(),
            KeyPersonRole = SemanticValueExtensions.NormalizeMeaningfulOrNull(keyPersonRole),
            MentionCount = mentionCount,
            InternalConnectionWeight = internalConnectionWeight,
            BridgeWeight = bridgeWeight,
            SortOrder = sortOrder
        };
    }

    public void AddFrequency(string frequency, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(frequency))
            return;

        Frequencies.Add(TopologySnapshotGroupFrequency.Create(Id, frequency, sortOrder));
    }

    public void AddMember(
        string name,
        string? role,
        int mentionCount,
        int uniquePartnerCount,
        int connectionWeight,
        DateTime lastSeenAt,
        int groupCount,
        bool isSharedAcrossGroups,
        bool isKeyPerson,
        int sortOrder)
    {
        Members.Add(TopologySnapshotGroupMember.Create(
            Id,
            name,
            role,
            mentionCount,
            uniquePartnerCount,
            connectionWeight,
            lastSeenAt,
            groupCount,
            isSharedAcrossGroups,
            isKeyPerson,
            sortOrder));
    }

    public void AddAction(string name, bool isPrimary, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        Actions.Add(TopologySnapshotGroupAction.Create(Id, name, isPrimary, sortOrder));
    }

    public TopologySnapshotBridge AddBridge(
        string targetGroupKey,
        string? targetDivision,
        string contactPersonName,
        string bridgeFrequency,
        int weight,
        string? primaryAction,
        int sortOrder)
    {
        var bridge = TopologySnapshotBridge.Create(
            Id,
            targetGroupKey,
            targetDivision,
            contactPersonName,
            bridgeFrequency,
            weight,
            primaryAction,
            sortOrder);

        Bridges.Add(bridge);
        return bridge;
    }
}
