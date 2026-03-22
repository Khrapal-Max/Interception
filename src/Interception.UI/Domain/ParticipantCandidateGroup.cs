//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;
using Interception.UI.Domain.Records;
using Interception.UI.Extensions;

namespace Interception.UI.Domain;

public class ParticipantCandidateGroup
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public List<ParticipantRef> ParticipantRefs { get; private set; } = [];
    public double ConfidenceScore { get; private set; }
    public string? SuggestedName { get; private set; }
    public PatternMatchReasons Reasons { get; private set; } = new();
    public CandidateGroupStatus Status { get; private set; } = CandidateGroupStatus.Open;
    public string? ResolvedBy { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static ParticipantCandidateGroup Create(
        IReadOnlyList<ParticipantRef> refs,
        double confidenceScore,
        PatternMatchReasons reasons,
        string? suggestedName = null)
    {
        ArgumentNullException.ThrowIfNull(refs);
        ArgumentNullException.ThrowIfNull(reasons);

        if (refs.Count < 2)
            throw new ArgumentException("Група повинна містити щонайменше два учасники.", nameof(refs));

        if (confidenceScore is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(confidenceScore), "Значення має бути від 0.0 до 1.0.");

        return new ParticipantCandidateGroup
        {
            ParticipantRefs = [.. refs],
            ConfidenceScore = confidenceScore,
            Reasons = reasons,
            SuggestedName = NormalizeOptional(suggestedName),
            CreatedAt = DateTimeConverter.Now
        };
    }

    public void Confirm(string resolvedName, string resolvedBy)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(resolvedName))
            throw new ArgumentException("Ім'я для підтвердження обов'язкове.", nameof(resolvedName));
        if (string.IsNullOrWhiteSpace(resolvedBy))
            throw new ArgumentException("Ідентифікатор оператора обов'язковий.", nameof(resolvedBy));

        SuggestedName = resolvedName.Trim();
        Status = CandidateGroupStatus.Confirmed;
        ResolvedBy = resolvedBy.Trim();
        ResolvedAt = DateTimeConverter.Now;
    }

    public void Dismiss(string resolvedBy)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(resolvedBy))
            throw new ArgumentException("Ідентифікатор оператора обов'язковий.", nameof(resolvedBy));

        Status = CandidateGroupStatus.Dismissed;
        ResolvedBy = resolvedBy.Trim();
        ResolvedAt = DateTimeConverter.Now;
    }

    public void UpdateSuggestedName(string? name)
    {
        EnsureOpen();
        SuggestedName = NormalizeOptional(name);
    }

    private void EnsureOpen()
    {
        if (Status != CandidateGroupStatus.Open)
            throw new InvalidOperationException($"Неможливо змінити групу зі статусом '{Status}'.");
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
