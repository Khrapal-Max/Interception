//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Domain;

/// <summary>
/// Analytical interpretation of an observation action.
/// Does not replace the raw action text and may coexist with catalog binding.
/// </summary>
public sealed class ObservationProbableAction
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ObservationId { get; private set; }
    public Observation Observation { get; private set; } = default!;

    public Guid ObservationActionId { get; private set; }
    public ObservationAction ObservationAction { get; private set; } = default!;

    public decimal Confidence { get; private set; }
    public string? Reason { get; private set; }
    public ProbableActionSource Source { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? CreatedBy { get; private set; }

    private ObservationProbableAction()
    {
    }

    internal static ObservationProbableAction Create(
        Guid observationId,
        Guid observationActionId,
        decimal confidence,
        string? reason,
        ProbableActionSource source,
        string? createdBy)
    {
        if (observationId == Guid.Empty)
            throw new ArgumentException("Observation id is required.", nameof(observationId));
        if (observationActionId == Guid.Empty)
            throw new ArgumentException("Observation action id is required.", nameof(observationActionId));
        EnsureConfidence(confidence);

        return new ObservationProbableAction
        {
            ObservationId = observationId,
            ObservationActionId = observationActionId,
            Confidence = confidence,
            Reason = NormalizeOptional(reason),
            Source = source,
            CreatedBy = NormalizeOptional(createdBy)
        };
    }

    public void Update(decimal confidence, string? reason)
    {
        EnsureConfidence(confidence);
        Confidence = confidence;
        Reason = NormalizeOptional(reason);
    }

    private static void EnsureConfidence(decimal confidence)
    {
        if (confidence < 0m || confidence > 1m)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
