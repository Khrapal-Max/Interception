//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Аналітичний зв'язок структурного керування між двома особами.
/// На кожному боці може бути або канонічна особа, або окремий підтверджений запис.
/// </summary>
public sealed class PersonDirectiveRelation
{
    public Guid Id { get; private set; }

    public Guid? FromCanonicalPersonId { get; private set; }
    public CanonicalPerson? FromCanonicalPerson { get; private set; }

    public Guid? FromResolvedParticipantId { get; private set; }
    public ResolvedParticipant? FromResolvedParticipant { get; private set; }

    public Guid? ToCanonicalPersonId { get; private set; }
    public CanonicalPerson? ToCanonicalPerson { get; private set; }

    public Guid? ToResolvedParticipantId { get; private set; }
    public ResolvedParticipant? ToResolvedParticipant { get; private set; }

    public DirectiveRelationType RelationType { get; private set; }
    public DirectiveRelationConfidence Confidence { get; private set; }
    public Guid? SourceObservationId { get; private set; }
    public bool IsManual { get; private set; }
    public string? Comment { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static PersonDirectiveRelation Create(
        Guid? fromCanonicalPersonId,
        Guid? fromResolvedParticipantId,
        Guid? toCanonicalPersonId,
        Guid? toResolvedParticipantId,
        DirectiveRelationType relationType,
        DirectiveRelationConfidence confidence,
        Guid? sourceObservationId,
        bool isManual,
        string? comment)
    {
        Validate(
            fromCanonicalPersonId,
            fromResolvedParticipantId,
            toCanonicalPersonId,
            toResolvedParticipantId);

        var now = ConverterDateTimeExtensions.Now;

        return new PersonDirectiveRelation
        {
            Id = Guid.NewGuid(),
            FromCanonicalPersonId = Normalize(fromCanonicalPersonId),
            FromResolvedParticipantId = Normalize(fromResolvedParticipantId),
            ToCanonicalPersonId = Normalize(toCanonicalPersonId),
            ToResolvedParticipantId = Normalize(toResolvedParticipantId),
            RelationType = relationType,
            Confidence = confidence,
            SourceObservationId = sourceObservationId,
            IsManual = isManual,
            Comment = SemanticValueExtensions.NormalizeMeaningfulOrNull(comment),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(
        DirectiveRelationType relationType,
        DirectiveRelationConfidence confidence,
        Guid? sourceObservationId,
        bool isManual,
        string? comment)
    {
        RelationType = relationType;
        Confidence = confidence;
        SourceObservationId = Normalize(sourceObservationId);
        IsManual = isManual;
        Comment = SemanticValueExtensions.NormalizeMeaningfulOrNull(comment);
        UpdatedAtUtc = ConverterDateTimeExtensions.Now;
    }

    private static Guid? Normalize(Guid? value)
        => value.HasValue && value.Value != Guid.Empty ? value.Value : null;

    private static void Validate(
        Guid? fromCanonicalPersonId,
        Guid? fromResolvedParticipantId,
        Guid? toCanonicalPersonId,
        Guid? toResolvedParticipantId)
    {
        fromCanonicalPersonId = Normalize(fromCanonicalPersonId);
        fromResolvedParticipantId = Normalize(fromResolvedParticipantId);
        toCanonicalPersonId = Normalize(toCanonicalPersonId);
        toResolvedParticipantId = Normalize(toResolvedParticipantId);

        var fromCount = (fromCanonicalPersonId.HasValue ? 1 : 0) + (fromResolvedParticipantId.HasValue ? 1 : 0);
        var toCount = (toCanonicalPersonId.HasValue ? 1 : 0) + (toResolvedParticipantId.HasValue ? 1 : 0);

        if (fromCount != 1)
            throw new InvalidOperationException("Потрібно вибрати рівно одну особу-джерело.");

        if (toCount != 1)
            throw new InvalidOperationException("Потрібно вибрати рівно одну особу-отримувача.");

        var sameCanonical = fromCanonicalPersonId.HasValue
            && toCanonicalPersonId.HasValue
            && fromCanonicalPersonId == toCanonicalPersonId;

        var sameResolved = fromResolvedParticipantId.HasValue
            && toResolvedParticipantId.HasValue
            && fromResolvedParticipantId == toResolvedParticipantId;

        if (sameCanonical || sameResolved)
            throw new InvalidOperationException("Неможливо створити структурний зв'язок особи із самою собою.");
    }
}
