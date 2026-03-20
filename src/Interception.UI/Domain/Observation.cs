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
/// Raw fact stays separate from later analytical enrichment.
/// </summary>
public sealed class Observation
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public DateTime ObservedDate { get; private set; }

    /// <summary>
    /// Optional bound action from the action catalog.
    /// Raw action text remains the source snapshot and is not overwritten by binding.
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

    public string? SubdivisionRaw { get; private set; }
    public string? SubdivisionNorm { get; private set; }
    public SubdivisionLinkStrength? SubdivisionStrength { get; private set; }
    public ObservationSubdivisionSource? SubdivisionSource { get; private set; }

    public string? Note { get; private set; }

    public string Source { get; private set; } = "manual";
    public Guid? SourceFileId { get; private set; }
    public int? SourceRow { get; private set; }

    public string ContentHash { get; private set; } = default!;

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }

    public List<ObservationParticipant> Participants { get; private set; } = [];
    public List<ObservationTag> Tags { get; private set; } = [];
    public List<ObservationProbableAction> ProbableActions { get; private set; } = [];

    private Observation()
    {
    }

    public static Observation Create(
        DateTime observedDate,
        string actionRaw,
        string? layer = null,
        string? rmRaw = null,
        string? pointRaw = null,
        string? locationRaw = null,
        string? districtRaw = null,
        string? subdivisionRaw = null,
        SubdivisionLinkStrength? subdivisionStrength = null,
        ObservationSubdivisionSource? subdivisionSource = null,
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

        observation.UpdateSubdivision(subdivisionRaw, subdivisionStrength, subdivisionSource);
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

    public ObservationTag AddTag(
        string rawValue,
        TagKind kind,
        ObservationTagSource source = ObservationTagSource.Manual,
        Guid? tagCatalogId = null)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            throw new ArgumentException("Tag value is required.", nameof(rawValue));

        var norm = TextNorm.NormalizeRequired(rawValue);
        var duplicate = Tags.Any(x => x.Kind == kind && x.RawValueNorm == norm);
        if (duplicate)
            throw new InvalidOperationException($"Tag '{rawValue}' already exists for this observation.");

        var tag = ObservationTag.Create(Id, rawValue, kind, source, tagCatalogId);
        Tags.Add(tag);
        return tag;
    }

    public void UpdateTag(Guid tagId, string rawValue, TagKind kind, Guid? tagCatalogId = null)
    {
        var tag = Tags.FirstOrDefault(x => x.Id == tagId)
            ?? throw new InvalidOperationException("Tag was not found.");

        var norm = TextNorm.NormalizeRequired(rawValue);
        var duplicate = Tags.Any(x => x.Id != tagId && x.Kind == kind && x.RawValueNorm == norm);
        if (duplicate)
            throw new InvalidOperationException($"Tag '{rawValue}' already exists for this observation.");

        tag.Update(rawValue, kind, tagCatalogId);
    }

    public void RemoveTag(Guid tagId)
    {
        var tag = Tags.FirstOrDefault(x => x.Id == tagId)
            ?? throw new InvalidOperationException("Tag was not found.");

        Tags.Remove(tag);
    }

    public ObservationProbableAction AddProbableAction(
        Guid observationActionId,
        decimal confidence,
        string? reason = null,
        ProbableActionSource source = ProbableActionSource.Manual,
        string? createdBy = null)
    {
        var duplicate = ProbableActions.Any(x => x.ObservationActionId == observationActionId);
        if (duplicate)
            throw new InvalidOperationException("Probable action already exists for this observation.");

        var probableAction = ObservationProbableAction.Create(
            Id,
            observationActionId,
            confidence,
            reason,
            source,
            createdBy);

        ProbableActions.Add(probableAction);
        return probableAction;
    }

    public void UpdateProbableAction(Guid probableActionId, decimal confidence, string? reason)
    {
        var probableAction = ProbableActions.FirstOrDefault(x => x.Id == probableActionId)
            ?? throw new InvalidOperationException("Probable action was not found.");

        probableAction.Update(confidence, reason);
    }

    public void RemoveProbableAction(Guid probableActionId)
    {
        var probableAction = ProbableActions.FirstOrDefault(x => x.Id == probableActionId)
            ?? throw new InvalidOperationException("Probable action was not found.");

        ProbableActions.Remove(probableAction);
    }

    public void BindAction(Guid actionId)
    {
        if (actionId == Guid.Empty)
            throw new ArgumentException("Action id is required.", nameof(actionId));

        ObservationActionId = actionId;
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

    public void UpdateTiming(DateTime observedDate)
    {
        ObservedDate = observedDate;
        RecomputeContentHash();
    }

    public void UpdateSubdivision(
        string? subdivisionRaw,
        SubdivisionLinkStrength? subdivisionStrength,
        ObservationSubdivisionSource? subdivisionSource)
    {
        SubdivisionRaw = NormalizeOptional(subdivisionRaw);
        SubdivisionNorm = TextNorm.Normalize(SubdivisionRaw);
        SubdivisionStrength = SubdivisionNorm is null ? null : subdivisionStrength;
        SubdivisionSource = SubdivisionNorm is null ? null : subdivisionSource;
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
        builder.Append(ObservedDate.ToString("yyyy-MM-dd HH^mm")).Append('|');
        builder.Append(ActionNorm).Append('|');
        builder.Append(TextNorm.Normalize(LocationRaw) ?? string.Empty).Append('|');
        builder.Append(TextNorm.Normalize(DistrictRaw) ?? string.Empty).Append('|');
        builder.Append(TextNorm.Normalize(RmRaw) ?? string.Empty).Append('|');
        builder.Append(TextNorm.Normalize(PointRaw) ?? string.Empty).Append('|');
        builder.Append(TextNorm.Normalize(Layer) ?? string.Empty).Append('|');
        builder.Append(SubdivisionNorm ?? string.Empty).Append('|');
        builder.Append(SubdivisionStrength is null ? string.Empty : ((short)SubdivisionStrength.Value).ToString()).Append('|');
        builder.Append(SubdivisionSource is null ? string.Empty : ((short)SubdivisionSource.Value).ToString()).Append('|');

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
