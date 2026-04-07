//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Run побудови snapshot-а топології карти зв'язків.
/// </summary>
public sealed class TopologySnapshotRun
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTime? DateFromUtc { get; private set; }
    public DateTime? DateToUtc { get; private set; }
    public TopologySnapshotRunStatus Status { get; private set; } = TopologySnapshotRunStatus.Building;
    public bool IsStale { get; private set; }
    public int GroupCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public List<TopologySnapshotGroup> Groups { get; private set; } = [];

    public static TopologySnapshotRun Create(DateTime? dateFromUtc = null, DateTime? dateToUtc = null)
    {
        return new TopologySnapshotRun
        {
            DateFromUtc = dateFromUtc,
            DateToUtc = dateToUtc,
            CreatedAt = ConverterDateTimeExtensions.Now,
            Status = TopologySnapshotRunStatus.Building,
            IsStale = false,
            GroupCount = 0,
            ErrorMessage = null,
            CompletedAt = null
        };
    }

    public void AddGroup(TopologySnapshotGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        Groups.Add(group);
    }

    public void Complete(int groupCount)
    {
        if (groupCount < 0)
            throw new ArgumentOutOfRangeException(nameof(groupCount));

        GroupCount = groupCount;
        Status = TopologySnapshotRunStatus.Completed;
        IsStale = false;
        ErrorMessage = null;
        CompletedAt = ConverterDateTimeExtensions.Now;
    }

    public void Fail(string? errorMessage)
    {
        Status = TopologySnapshotRunStatus.Failed;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();
        CompletedAt = ConverterDateTimeExtensions.Now;
    }

    public void MarkStale()
    {
        if (Status == TopologySnapshotRunStatus.Completed)
            IsStale = true;
    }
}
