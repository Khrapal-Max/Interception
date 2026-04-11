//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain.Entities;

/// <summary>
/// Учасник snapshot-групи.
/// </summary>
public sealed class TopologySnapshotGroupMember
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid GroupId { get; private set; }
    public TopologySnapshotGroup? Group { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string? Role { get; private set; }
    public int MentionCount { get; private set; }
    public int UniquePartnerCount { get; private set; }
    public int ConnectionWeight { get; private set; }
    public DateTime LastSeenAt { get; private set; }
    public int GroupCount { get; private set; }
    public bool IsSharedAcrossGroups { get; private set; }
    public bool IsKeyPerson { get; private set; }
    public int SortOrder { get; private set; }

    public static TopologySnapshotGroupMember Create(
        Guid groupId,
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
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name обов'язкове.", nameof(name));

        return new TopologySnapshotGroupMember
        {
            GroupId = groupId,
            Name = name.Trim(),
            Role = SemanticValueExtensions.NormalizeMeaningfulOrNull(role),
            MentionCount = mentionCount,
            UniquePartnerCount = uniquePartnerCount,
            ConnectionWeight = connectionWeight,
            LastSeenAt = lastSeenAt,
            GroupCount = groupCount,
            IsSharedAcrossGroups = isSharedAcrossGroups,
            IsKeyPerson = isKeyPerson,
            SortOrder = sortOrder
        };
    }
}
