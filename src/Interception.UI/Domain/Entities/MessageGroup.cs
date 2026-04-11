//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Records;

namespace Interception.UI.Domain.Entities;

/// <summary>
/// Група повідомлень, які DailyReportService вважає пов'язаними між собою
/// за спільними сигнальними ознаками (частота, вектор, підрозділ тощо).
///
/// Одне повідомлення може входити лише в одну групу в межах одного звіту.
/// </summary>
public class MessageGroup
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// FK до DailyReport, якому належить ця група.
    /// </summary>
    public Guid DailyReportId { get; private set; }
    public DailyReport DailyReport { get; private set; } = default!;

    /// <summary>
    /// Людино-читабельний ключ групи, наприклад "149.500 / NE".
    /// Формується сервісом як Frequency + VectorSignal.
    /// </summary>
    public string? GroupKey { get; private set; }

    public string? CommonFrequency { get; private set; }
    public string? CommonVector { get; private set; }
    public string? CommonDivision { get; private set; }

    /// <summary>
    /// Посилання на повідомлення, що входять до групи.
    /// Зберігаються як value object, без навігаційної властивості,
    /// щоб не завантажувати весь граф при читанні звіту.
    /// </summary>
    public List<MessageGroupEntry> Entries { get; private set; } = [];

    // -----------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------

    internal static MessageGroup Create(
        Guid dailyReportId,
        string? commonFrequency,
        string? commonVector,
        string? commonDivision,
        IEnumerable<Guid> messageIds)
    {
        var group = new MessageGroup
        {
            DailyReportId = dailyReportId,
            CommonFrequency = NormalizeOptional(commonFrequency),
            CommonVector = NormalizeOptional(commonVector),
            CommonDivision = NormalizeOptional(commonDivision),
        };

        group.GroupKey = BuildGroupKey(group.CommonFrequency, group.CommonVector);

        foreach (var msgId in messageIds)
            group.Entries.Add(new MessageGroupEntry(group.Id, msgId));

        return group;
    }

    // -----------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------

    private static string? BuildGroupKey(string? frequency, string? vector)
    {
        var parts = new[] { frequency, vector }
            .Where(p => p is not null);
        var key = string.Join(" / ", parts);
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
