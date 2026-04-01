//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Analytics.Dtos;

/// <summary>
/// Suggestion для поля Частота.
/// Містить найчастіші пари для цієї частоти з бази даних:
///   — Division     → показується як hint в dropdown (оператор бачить контекст до вибору)
///   — VectorSignal → підставляється автоматично при виборі частоти
/// </summary>
public sealed class FrequencySuggestionDto
{
    public string Frequency { get; init; } = default!;

    /// <summary>Найчастіший підрозділ для цієї частоти.</summary>
    public string? Division { get; init; }

    /// <summary>Найчастіший вектор сигналу для цієї частоти.</summary>
    public string? VectorSignal { get; init; }

    /// <summary>Загальна кількість повідомлень — для сортування за популярністю.</summary>
    public int Count { get; init; }
}
