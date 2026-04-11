//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Analytics;

/// <summary>
/// Перелік ознак за якими PatternRecognitionService визначив схожість між учасниками.
/// Використовується як пояснення до ConfidenceScore — оператор бачить
/// чому система згрупувала саме цих учасників.
/// </summary>
public class PatternMatchReasons
{
    /// <summary>Збіг частоти сигналу (Frequency). Сильна ознака.</summary>
    public bool SameFrequency { get; init; }

    /// <summary>Збіг вектора сигналу (VectorSignal). Сильна ознака.</summary>
    public bool SameVector { get; init; }

    /// <summary>Збіг точки фіксації сигналу (PointSignal).</summary>
    public bool SamePointSignal { get; init; }

    /// <summary>Збіг підрозділу (Division).</summary>
    public bool SameDivision { get; init; }

    /// <summary>
    /// Повідомлення зафіксовані в межах допустимого часового вікна.
    /// Порогове значення налаштовується через PatternRecognitionOptions.
    /// </summary>
    public bool CloseInTime { get; init; }

    /// <summary>
    /// НВ регулярно спілкується з тими самими відомими учасниками.
    /// Наприклад: завжди з ШАПКА і ВОЛГА → швидше за все з їх підрозділу.
    /// Сильна ознака при ≥2 спільних партнерах.
    /// </summary>
    public bool SharedPartners { get; init; }

    /// <summary>
    /// Збіг міток у спостереженнях (наприклад, однаковий населений пункт,
    /// тип операції тощо). Слабка ситуаційна ознака.
    /// </summary>
    public bool SharedLabels { get; init; }

    /// <summary>
    /// Повертає кількість підтверджених ознак збігу.
    /// Використовується для обчислення ConfidenceScore.
    /// </summary>
    public int MatchCount =>
        (SameFrequency ? 1 : 0) +
        (SameVector ? 1 : 0) +
        (SamePointSignal ? 1 : 0) +
        (SameDivision ? 1 : 0) +
        (CloseInTime ? 1 : 0) +
        (SharedPartners ? 1 : 0) +
        (SharedLabels ? 1 : 0);
}
