//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain;

/// <summary>
/// Перелік ознак, за якими PatternRecognitionService визначив схожість між учасниками.
/// Використовується як пояснення до ConfidenceScore — оператор бачить,
/// чому система згрупувала саме цих учасників.
/// </summary>
public class PatternMatchReasons
{
    /// <summary>
    /// Збіг частоти сигналу (Frequency).
    /// </summary>
    public bool SameFrequency { get; init; }

    /// <summary>
    /// Збіг вектора сигналу (VectorSignal).
    /// </summary>
    public bool SameVector { get; init; }

    /// <summary>
    /// Збіг точки фіксації сигналу (PointSignal).
    /// </summary>
    public bool SamePointSignal { get; init; }

    /// <summary>
    /// Збіг підрозділу (Division).
    /// </summary>
    public bool SameDivision { get; init; }

    /// <summary>
    /// Повідомлення зафіксовані в межах допустимого часового вікна.
    /// Порогове значення налаштовується через PatternRecognitionOptions.
    /// </summary>
    public bool CloseInTime { get; init; }

    /// <summary>
    /// Повертає кількість підтверджених ознак збігу.
    /// Використовується для обчислення ConfidenceScore.
    /// </summary>
    public int MatchCount =>
        (SameFrequency ? 1 : 0) +
        (SameVector ? 1 : 0) +
        (SamePointSignal ? 1 : 0) +
        (SameDivision ? 1 : 0) +
        (CloseInTime ? 1 : 0);
}
