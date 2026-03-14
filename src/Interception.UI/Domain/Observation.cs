//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;
using System.Security.Cryptography;
using System.Text;

namespace Interception.UI.Domain;

/// <summary>
/// Primary raw observation record.
/// Stores the fact as it was fixed by operator/import and allows lightweight in-record editing of participants.
/// </summary>
public sealed class Observation
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public DateOnly ObservedDate { get; private set; }
    public DayPart DayPart { get; private set; }

    /// <summary>
    /// Optional bound action from the action catalog.
    /// Raw action text still remains the source snapshot.
    /// </summary>
    public Guid? ObservationActionId { get; private set; }
    public ObservationAction? ObservationAction { get; private set; }

    public string? Layer { get; private set; }
    public string? RmRaw { get; private set; }
    public string? PointRaw { get; private set; }
    public string? LocationRaw { get; private set; }
    public string? DistrictRaw { get; private set; }

    public string ActionRaw { get; private set; } = default!;
    public string ActionNorm { get; private set; } = default!;

    public string? Note { get; private set; }

    public string Source { get; private set; } = "manual";
    public Guid? SourceFileId { get; private set; }
    public int? SourceRow { get; private set; }

    public string ContentHash { get; private set; } = default!;

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }

    public List<ObservationParticipant> Participants { get; private set; } = [];

    private Observation()
    {
    }

    public static Observation Create(
        DateOnly observedDate,
        DayPart dayPart,
        string actionRaw,
        string? layer = null,
        string? rmRaw = null,
        string? pointRaw = null,
        string? locationRaw = null,
        string? districtRaw = null,
        string? note = null,
        string source = "manual",
        Guid? sourceFileId = null,
        int? sourceRow = null,
        string? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(actionRaw))
            throw new ArgumentException("Action is required.", nameof(actionRaw));

        var observation = new Observation
        {
            ObservedDate = observedDate,
            DayPart = dayPart,
            Layer = NormalizeOptional(layer),
            RmRaw = NormalizeOptional(rmRaw),
            PointRaw = NormalizeOptional(pointRaw),
            LocationRaw = NormalizeOptional(locationRaw),
            DistrictRaw = NormalizeOptional(districtRaw),
            ActionRaw = actionRaw.Trim(),
            ActionNorm = TextNorm.NormalizeRequired(actionRaw),
            Note = NormalizeOptional(note),
            Source = string.IsNullOrWhiteSpace(source) ? "manual" : source.Trim(),
            SourceFileId = sourceFileId,
            SourceRow = sourceRow,
            CreatedBy = NormalizeOptional(createdBy)
        };

        observation.RecomputeContentHash();
        return observation;
    }

    public ObservationParticipant AddParticipant(string? labelRaw, bool isUnknown, string? roleRaw = null, int? ordinal = null)
    {
        var nextOrdinal = ordinal ?? (Participants.Count == 0 ? 1 : Participants.Max(p => p.Ordinal) + 1);
        EnsureKnownParticipantUniqueness(null, labelRaw, isUnknown);

        var participant = new ObservationParticipant(Id, labelRaw, isUnknown, roleRaw, nextOrdinal);
        Participants.Add(participant);
        RecomputeContentHash();
        return participant;
    }

    public void UpdateParticipant(Guid participantId, string? labelRaw, bool isUnknown, string? roleRaw)
    {
        var participant = Participants.FirstOrDefault(x => x.Id == participantId)
            ?? throw new InvalidOperationException("Participant was not found.");

        EnsureKnownParticipantUniqueness(participantId, labelRaw, isUnknown);
        participant.UpdateSnapshot(labelRaw, isUnknown, roleRaw);
        RecomputeContentHash();
    }

    public void RemoveParticipant(Guid participantId)
    {
        var participant = Participants.FirstOrDefault(x => x.Id == participantId)
            ?? throw new InvalidOperationException("Participant was not found.");

        Participants.Remove(participant);
        RecomputeContentHash();
    }

    public void BindAction(Guid actionId, string actionName)
    {
        if (actionId == Guid.Empty)
            throw new ArgumentException("Action id is required.", nameof(actionId));
        if (string.IsNullOrWhiteSpace(actionName))
            throw new ArgumentException("Action name is required.", nameof(actionName));

        ObservationActionId = actionId;
        ActionRaw = actionName.Trim();
        ActionNorm = TextNorm.NormalizeRequired(actionName);
        RecomputeContentHash();
    }

    public void ClearBoundAction()
    {
        ObservationActionId = null;
    }

    public void UpdateContext(
        string? actionRaw,
        string? layer,
        string? rmRaw,
        string? pointRaw,
        string? locationRaw,
        string? districtRaw,
        string? note)
    {
        if (!string.IsNullOrWhiteSpace(actionRaw))
        {
            ActionRaw = actionRaw.Trim();
            ActionNorm = TextNorm.NormalizeRequired(actionRaw);
        }

        Layer = NormalizeOptional(layer);
        RmRaw = NormalizeOptional(rmRaw);
        PointRaw = NormalizeOptional(pointRaw);
        LocationRaw = NormalizeOptional(locationRaw);
        DistrictRaw = NormalizeOptional(districtRaw);
        Note = NormalizeOptional(note);

        RecomputeContentHash();
    }

    private void EnsureKnownParticipantUniqueness(Guid? currentParticipantId, string? labelRaw, bool isUnknown)
    {
        var norm = TextNorm.Normalize(labelRaw);
        if (isUnknown || norm is null)
            return;

        var duplicate = Participants.Any(p =>
            p.Id != currentParticipantId &&
            !p.IsUnknown &&
            string.Equals(p.LabelNorm, norm, StringComparison.Ordinal));

        if (duplicate)
            throw new InvalidOperationException($"Participant '{labelRaw}' already exists in this observation.");
    }

    private void RecomputeContentHash()
    {
        ContentHash = ComputeContentHash();
    }

    private string ComputeContentHash()
    {
        var builder = new StringBuilder();
        builder.Append(ObservedDate.ToString("yyyy-MM-dd")).Append('|');
        builder.Append((short)DayPart).Append('|');
        builder.Append(ActionNorm).Append('|');
        builder.Append(TextNorm.Normalize(LocationRaw) ?? string.Empty).Append('|');
        builder.Append(TextNorm.Normalize(DistrictRaw) ?? string.Empty).Append('|');
        builder.Append(TextNorm.Normalize(RmRaw) ?? string.Empty).Append('|');
        builder.Append(TextNorm.Normalize(PointRaw) ?? string.Empty).Append('|');
        builder.Append(TextNorm.Normalize(Layer) ?? string.Empty).Append('|');

        foreach (var participant in Participants.OrderBy(x => x.Ordinal))
        {
            var roleNorm = TextNorm.Normalize(participant.RoleRaw) ?? string.Empty;

            if (participant.IsUnknown)
            {
                builder.Append("u:")
                    .Append(participant.Ordinal)
                    .Append(':')
                    .Append(roleNorm)
                    .Append(';');

                continue;
            }

            builder.Append("k:")
                .Append(participant.LabelNorm ?? string.Empty)
                .Append(':')
                .Append(roleNorm)
                .Append(';');
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
