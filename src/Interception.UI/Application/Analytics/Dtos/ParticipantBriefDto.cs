//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>Скорочений опис учасника для відображення у списку.</summary>
public sealed class ParticipantBriefDto
{
    public string? Name { get; init; }
    public string? Role { get; init; }
    public bool IsUnknown { get; init; }
    public int Ordinal { get; init; }

    /// <summary>
    /// Встановлена назва з ResolvedParticipant (overlay).
    /// Null якщо учасник ще не ідентифікований або не НВ.
    /// </summary>
    public string? ResolvedName { get; init; }

    /// <summary>True якщо оператор підтвердив ідентифікацію (✓), false якщо гіпотеза (?).</summary>
    public bool IsResolved { get; init; }

    /// <summary>
    /// Відображувана назва у реєстрі:
    ///   ШАПКА          — відомий учасник
    ///   НВ             — невідомий без overlay
    ///   НВ [= ШАПКА ✓] — підтверджений overlay
    ///   НВ [= ШАПКА ?] — гіпотеза overlay
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (!IsUnknown) return Name ?? "НВ";

            if (!string.IsNullOrWhiteSpace(ResolvedName))
            {
                var marker = IsResolved ? "✓" : "?";
                return $"НВ [= {ResolvedName} {marker}]";
            }

            return "НВ";
        }
    }
}
