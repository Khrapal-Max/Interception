//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Extensions;

namespace Interception.UI.Domain;

/// <summary>
/// Об’єднаний профіль — аналітичне ядро людини, що може об'єднувати кілька
/// підтверджених registry rows в одну особу.
/// </summary>
public sealed class CanonicalPerson
{
    public Guid Id { get; private set; }
    public string DisplayName { get; private set; } = default!;
    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public List<CanonicalPersonMember> Members { get; private set; } = [];

    public static CanonicalPerson Create(string displayName, string? note = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Назва основної особи обов'язкова.", nameof(displayName));

        var now = ConverterDateTimeExtensions.Now;

        return new CanonicalPerson
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName.Trim(),
            Note = SemanticValueExtensions.NormalizeMeaningfulOrNull(note),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(string displayName, string? note)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Назва основної особи обов'язкова.", nameof(displayName));

        DisplayName = displayName.Trim();
        Note = SemanticValueExtensions.NormalizeMeaningfulOrNull(note);
        UpdatedAtUtc = ConverterDateTimeExtensions.Now;
    }

    public void UpdateNote(string? note)
    {
        Note = SemanticValueExtensions.NormalizeMeaningfulOrNull(note);
        UpdatedAtUtc = ConverterDateTimeExtensions.Now;
    }

    public CanonicalPersonMember AddMember(Guid resolvedParticipantId)
    {
        if (Members.Any(x => x.ResolvedParticipantId == resolvedParticipantId))
            throw new InvalidOperationException("Цей підтверджений запис уже входить до основної особи.");

        var member = CanonicalPersonMember.Create(Id, resolvedParticipantId);
        Members.Add(member);
        UpdatedAtUtc = ConverterDateTimeExtensions.Now;
        return member;
    }
}
