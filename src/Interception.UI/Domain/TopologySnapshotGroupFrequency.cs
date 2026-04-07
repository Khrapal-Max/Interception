//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain;

/// <summary>
/// Частота snapshot-групи.
/// </summary>
public sealed class TopologySnapshotGroupFrequency
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid GroupId { get; private set; }
    public TopologySnapshotGroup? Group { get; private set; }
    public string Frequency { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    public static TopologySnapshotGroupFrequency Create(Guid groupId, string frequency, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(frequency))
            throw new ArgumentException("Frequency обов'язкова.", nameof(frequency));

        return new TopologySnapshotGroupFrequency
        {
            GroupId = groupId,
            Frequency = frequency.Trim(),
            SortOrder = sortOrder
        };
    }
}
