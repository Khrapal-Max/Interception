//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain;

public class InterceptionMessage
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public DateTime ObservedDate { get; private set; }
    public string? Frequency { get; private set; }
    public string? Division { get; set; }
    public string? PointSignal { get; private set; }
    public string? VectorSignal { get; private set; }

    public Guid? InterceptionActionId { get; private set; }
    public InterceptionAction? InterceptionAction { get; private set; }

    public string? Note { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public List<InterceptionMessageParticipant> Participants { get; private set; } = [];
    public List<InterceptionMessageLabel> Labels { get; private set; } = [];

    public static InterceptionMessage Create(
        DateTime observedDate,
        string? frequency,
        string? division,
        string? vectorSignal,
        InterceptionAction interceptionAction,
        string? note,
        string? createdBy,
        string? pointSignal = null)
    {
        ArgumentNullException.ThrowIfNull(interceptionAction);

        return new InterceptionMessage
        {
            ObservedDate = ConverterDateTimeExtensions.ToUtc(observedDate),
            Frequency = NormalizeOptional(frequency),
            Division = NormalizeOptional(division),
            VectorSignal = NormalizeOptional(vectorSignal),
            InterceptionAction = interceptionAction,
            InterceptionActionId = interceptionAction.Id,
            Note = NormalizeOptional(note),
            CreatedBy = NormalizeOptional(createdBy),
            CreatedAt = ConverterDateTimeExtensions.Now,
            PointSignal = NormalizeOptional(pointSignal)
        };
    }

    public void Update(
        DateTime observedDate,
        string? frequency,
        string? division,
        string? vectorSignal,
        InterceptionAction interceptionAction,
        string? note,
        string? pointSignal = null)
    {
        ArgumentNullException.ThrowIfNull(interceptionAction);

        ObservedDate = ConverterDateTimeExtensions.ToUtc(observedDate);
        Frequency = NormalizeOptional(frequency);
        Division = NormalizeOptional(division);
        VectorSignal = NormalizeOptional(vectorSignal);
        InterceptionAction = interceptionAction;
        InterceptionActionId = interceptionAction.Id;
        Note = NormalizeOptional(note);
        UpdatedAt = ConverterDateTimeExtensions.Now;
        PointSignal = NormalizeOptional(pointSignal);
    }

    public InterceptionMessageParticipant AddParticipant(
        string? name, bool isUnknown, string? role = null, int? ordinal = null)
    {
        var nextOrdinal = ordinal ?? (Participants.Count == 0
            ? 1
            : Participants.Max(p => p.Ordinal) + 1);

        EnsureKnownParticipantUniqueness(null, name, isUnknown);

        var participant = new InterceptionMessageParticipant(Id, name, isUnknown, role, nextOrdinal);
        Participants.Add(participant);
        return participant;
    }

    public void RemoveParticipant(Guid participantId)
    {
        var participant = Participants.FirstOrDefault(x => x.Id == participantId)
            ?? throw new InvalidOperationException("Учасника не знайдено.");
        Participants.Remove(participant);
    }

    public InterceptionMessageLabel AddLabel(string nameLabel)
    {
        if (string.IsNullOrWhiteSpace(nameLabel))
            throw new ArgumentException("Значення мітки обов'язкове.", nameof(nameLabel));

        var norm = StringTextNormExtensions.NormalizeRequired(nameLabel);
        if (Labels.Any(x => x.NameLabel == norm))
            throw new InvalidOperationException($"Мітка '{nameLabel}' вже існує.");

        var label = InterceptionMessageLabel.Create(nameLabel);
        Labels.Add(label);
        return label;
    }

    public void RemoveLabel(Guid labelId)
    {
        var label = Labels.FirstOrDefault(x => x.Id == labelId)
            ?? throw new InvalidOperationException("Мітку не знайдено.");
        Labels.Remove(label);
    }

    private void EnsureKnownParticipantUniqueness(Guid? currentId, string? name, bool isUnknown)
    {
        var norm = NormalizeOptional(name);
        if (isUnknown || norm is null) return;

        var duplicate = Participants.Any(p =>
            p.Id != currentId &&
            !p.IsUnknown &&
            string.Equals(p.Name, norm, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
            throw new InvalidOperationException($"Учасник '{name}' вже існує в цьому перехопленні.");
    }

    private static string? NormalizeOptional(string? value)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(value);
}
