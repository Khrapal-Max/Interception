//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain;

/// <summary>
/// Налаштування PatternRecognitionService.
/// Завантажуються з appsettings.json через IOptions&lt;PatternRecognitionOptions&gt;.
///
/// Приклад у appsettings.json:
/// <code>
/// "PatternRecognition": {
///   "FrequencyWeight"    : 0.30,
///   "VectorWeight"       : 0.25,
///   "SharedPartnersWeight": 0.20,
///   "PointSignalWeight"  : 0.10,
///   "DivisionWeight"     : 0.08,
///   "TimeWeight"         : 0.05,
///   "SharedLabelsWeight" : 0.02,
///   "TimeWindowMinutes"  : 30,
///   "MinSharedPartners"  : 2,
///   "MinConfidenceScore" : 0.50
/// }
/// </code>
/// </summary>
public class PatternRecognitionOptions
{
    public const string SectionName = "PatternRecognition";

    // ------------------------------------------------------------------
    // Ваги ознак (у сумі мають давати 1.0)
    // ------------------------------------------------------------------

    /// <summary>Вага збігу частоти сигналу. Сильна ознака.</summary>
    public double FrequencyWeight { get; set; } = 0.30;

    /// <summary>Вага збігу вектора сигналу. Сильна ознака.</summary>
    public double VectorWeight { get; set; } = 0.25;

    /// <summary>
    /// Вага спільних відомих партнерів.
    /// НВ що регулярно спілкується з тими самими особами → сильний сигнал.
    /// </summary>
    public double SharedPartnersWeight { get; set; } = 0.20;

    /// <summary>Вага збігу точки фіксації сигналу.</summary>
    public double PointSignalWeight { get; set; } = 0.10;

    /// <summary>Вага збігу підрозділу.</summary>
    public double DivisionWeight { get; set; } = 0.08;

    /// <summary>Вага близькості в часі.</summary>
    public double TimeWeight { get; set; } = 0.05;

    /// <summary>
    /// Вага збігу міток. Слабка ситуаційна ознака —
    /// однаковий населений пункт, тип операції тощо.
    /// </summary>
    public double SharedLabelsWeight { get; set; } = 0.02;

    // ------------------------------------------------------------------
    // Порогові значення
    // ------------------------------------------------------------------

    /// <summary>
    /// Максимальна різниця між ObservedDate двох повідомлень (хвилини)
    /// при якій CloseInTime = true.
    /// </summary>
    public int TimeWindowMinutes { get; set; } = 30;

    /// <summary>
    /// Мінімальна кількість спільних відомих партнерів
    /// щоб SharedPartners = true.
    /// </summary>
    public int MinSharedPartners { get; set; } = 2;

    /// <summary>
    /// Мінімальний ConfidenceScore для створення ParticipantCandidateGroup.
    /// Групи нижче цього порогу ігноруються.
    /// </summary>
    public double MinConfidenceScore { get; set; } = 0.50;

    // ------------------------------------------------------------------
    // Validation
    // ------------------------------------------------------------------

    /// <summary>
    /// Перевіряє що ваги в сумі дають 1.0 (±0.01 похибка округлення).
    /// </summary>
    public bool WeightSumIsValid()
    {
        var sum = FrequencyWeight + VectorWeight + SharedPartnersWeight
                + PointSignalWeight + DivisionWeight + TimeWeight + SharedLabelsWeight;
        return Math.Abs(sum - 1.0) <= 0.01;
    }
}
