//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;
using Interception.UI.Domain.Exceptions;
using Interception.UI.Domain.ValueObjects;
using Interception.UI.Extensions;

namespace Interception.UI.Domain.Entities;

public class ParticipantCandidateGroup
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public List<ParticipantRef> ParticipantRefs { get; private set; } = [];
    public double ConfidenceScore { get; private set; }

    /// <summary>
    /// Запропонований системою позивний / назва.
    /// Для Open групи може бути null, якщо достатньо впевненого кандидата ще немає.
    /// </summary>
    public string? SuggestedName { get; private set; }

    /// <summary>
    /// Запропонована системою роль.
    /// </summary>
    public string? SuggestedRole { get; private set; }

    /// <summary>
    /// Запропонований системою підрозділ.
    /// Зазвичай це домінуюче значення в групі або значення з підтвердженої особи.
    /// </summary>
    public string? SuggestedDivision { get; private set; }

    public PatternMatchReasons Reasons { get; private set; } = new();
    public CandidateGroupStatus Status { get; private set; } = CandidateGroupStatus.Open;

    /// <summary>
    /// Після підтвердження містить встановлену особу, якщо така була створена.
    /// </summary>
    public Guid? ResolvedParticipantId { get; private set; }

    public string? ResolvedBy { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static ParticipantCandidateGroup Create(
        IReadOnlyList<ParticipantRef> refs,
        double confidenceScore,
        PatternMatchReasons reasons,
        string? suggestedName = null,
        string? suggestedRole = null,
        string? suggestedDivision = null)
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
            SuggestedRole = NormalizeOptional(suggestedRole),
            SuggestedDivision = NormalizeOptional(suggestedDivision),
            CreatedAt = ConverterDateTimeExtensions.Now
        };
    }

    public void Confirm(string resolvedName, string resolvedBy, Guid? resolvedParticipantId = null)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(resolvedName))
            throw new ArgumentException("Ім'я для підтвердження обов'язкове.", nameof(resolvedName));
        if (string.IsNullOrWhiteSpace(resolvedBy))
            throw new ArgumentException("Ідентифікатор оператора обов'язковий.", nameof(resolvedBy));

        SuggestedName = resolvedName.Trim();
        Status = CandidateGroupStatus.Confirmed;
        ResolvedParticipantId = resolvedParticipantId;
        ResolvedBy = resolvedBy.Trim();
        ResolvedAt = ConverterDateTimeExtensions.Now;
    }

    public void Dismiss(string resolvedBy)
    {
        EnsureOpen();
        if (string.IsNullOrWhiteSpace(resolvedBy))
            throw new ArgumentException("Ідентифікатор оператора обов'язковий.", nameof(resolvedBy));

        Status = CandidateGroupStatus.Dismissed;
        ResolvedBy = resolvedBy.Trim();
        ResolvedAt = ConverterDateTimeExtensions.Now;
    }

    public void UpdateSuggestedName(string? name)
    {
        EnsureOpen();
        SuggestedName = NormalizeOptional(name);
    }

    public void UpdateSuggestedRole(string? role)
    {
        EnsureOpen();
        SuggestedRole = NormalizeOptional(role);
    }

    public void UpdateSuggestedDivision(string? division)
    {
        EnsureOpen();
        SuggestedDivision = NormalizeOptional(division);
    }

    /// <summary>
    /// Додає нове спостереження до групи (збагачення).
    /// Викликається під час RunAsync якщо новий НВ підходить до існуючої Open групи.
    /// </summary>
    public void AddRef(ParticipantRef newRef)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(newRef);

        if (ParticipantRefs.Any(r => r.ParticipantId == newRef.ParticipantId))
            return;

        ParticipantRefs.Add(newRef);
    }

    /// <summary>
    /// Оновлює ConfidenceScore і Reasons після перерахунку по всіх учасниках.
    /// Викликається після AddRef.
    /// </summary>
    public void UpdateScore(double confidenceScore, PatternMatchReasons reasons)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(reasons);

        if (confidenceScore is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(
                nameof(confidenceScore), "Значення має бути від 0.0 до 1.0.");

        ConfidenceScore = confidenceScore;
        Reasons = reasons;
    }

    private void EnsureOpen()
    {
        if (Status != CandidateGroupStatus.Open)
            throw new AggregateStateViolationException($"Неможливо змінити групу зі статусом '{Status}'.");
    }

    private static string? NormalizeOptional(string? value)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(value);
}
