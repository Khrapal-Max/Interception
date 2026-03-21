//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain;

/// <summary>
/// Налаштування PatternRecognitionService.
/// Виносяться в окремий клас, щоб їх можна було завантажити
/// з appsettings.json через IOptions&lt;PatternRecognitionOptions&gt;
/// і змінювати без перекомпіляції.
///
/// Приклад у appsettings.json:
/// <code>
/// "PatternRecognition": {
///   "FrequencyWeight"  : 0.35,
///   "VectorWeight"     : 0.30,
///   "PointSignalWeight": 0.15,
///   "DivisionWeight"   : 0.10,
///   "TimeWeight"       : 0.10,
///   "TimeWindowMinutes": 30,
///   "MinConfidenceScore": 0.50
/// }
/// </code>
/// </summary>
public class PatternRecognitionOptions
{
    public const string SectionName = "PatternRecognition";

    // ------------------------------------------------------------------
    // Ваги окремих ознак (у сумі мають давати 1.0)
    // ------------------------------------------------------------------

    /// <summary>Вага збігу частоти сигналу.</summary>
    public double FrequencyWeight { get; set; } = 0.35;

    /// <summary>Вага збігу вектора сигналу.</summary>
    public double VectorWeight { get; set; } = 0.30;

    /// <summary>Вага збігу точки фіксації сигналу.</summary>
    public double PointSignalWeight { get; set; } = 0.15;

    /// <summary>Вага збігу підрозділу.</summary>
    public double DivisionWeight { get; set; } = 0.10;

    /// <summary>Вага близькості в часі.</summary>
    public double TimeWeight { get; set; } = 0.10;

    // ------------------------------------------------------------------
    // Порогові значення
    // ------------------------------------------------------------------

    /// <summary>
    /// Максимальна різниця між ObservedDate двох повідомлень (у хвилинах),
    /// при якій CloseInTime = true.
    /// </summary>
    public int TimeWindowMinutes { get; set; } = 30;

    /// <summary>
    /// Мінімальний ConfidenceScore для створення ParticipantCandidateGroup.
    /// Групи нижче цього порогу ігноруються.
    /// </summary>
    public double MinConfidenceScore { get; set; } = 0.50;

    // ------------------------------------------------------------------
    // Validation helper
    // ------------------------------------------------------------------

    /// <summary>
    /// Перевіряє, що ваги в сумі дають 1.0 (±0.01 похибка округлення).
    /// Викликати при старті застосунку через IValidateOptions.
    /// </summary>
    public bool WeightSumIsValid()
    {
        var sum = FrequencyWeight + VectorWeight + PointSignalWeight
                + DivisionWeight + TimeWeight;
        return Math.Abs(sum - 1.0) <= 0.01;
    }
}
