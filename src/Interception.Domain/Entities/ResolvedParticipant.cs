//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Domain.Entities;

/// <summary>
/// Встановлена особа — аналітичний висновок про те хто ховається за НВ.
///
/// Не змінює оригінальні спостереження — є окремою аналітичною сутністю.
/// Створюється коли оператор підтверджує <see cref="ParticipantCandidateGroup"/>.
///
/// UI показує overlay в реєстрі спостережень:
///   НВ [= ШАПКА ?]  — гіпотеза, група ще Open
///   НВ [= ШАПКА ✓]  — підтверджено, ResolvedParticipant існує
/// </summary>
public sealed class ResolvedParticipant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Role { get; private set; }
    public string? Division { get; private set; }
    public string ConfirmedBy { get; private set; } = default!;
    public DateTime ConfirmedAt { get; private set; }

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    public static ResolvedParticipant Create(
        string name,
        string confirmedBy,
        string? role = null,
        string? division = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Ім'я встановленого учасника обов'язкове.", nameof(name));

        if (string.IsNullOrWhiteSpace(confirmedBy))
            throw new ArgumentException(
                "Ідентифікатор оператора обов'язковий.", nameof(confirmedBy));

        return new ResolvedParticipant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Role = NormalizeOptional(role),
            Division = NormalizeOptional(division),
            ConfirmedBy = confirmedBy.Trim(),
            ConfirmedAt = ConverterDateTimeExtensions.Now
        };
    }

    // -------------------------------------------------------------------------
    // Update
    // -------------------------------------------------------------------------

    public void Update(string name, string? role = null, string? division = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Ім'я встановленого учасника обов'язкове.", nameof(name));

        Name = name.Trim();
        Role = NormalizeOptional(role);
        Division = NormalizeOptional(division);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string? NormalizeOptional(string? value)
        => SemanticValueExtensions.NormalizeMeaningfulOrNull(value);
}
