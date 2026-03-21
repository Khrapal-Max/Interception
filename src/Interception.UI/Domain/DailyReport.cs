//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;
using Interception.UI.Extensions;

namespace Interception.UI.Domain;

public class DailyReport
{
    public Guid              Id            { get; private set; } = Guid.NewGuid();
    public DateOnly          ReportDate    { get; private set; }
    public int               TotalMessages { get; private set; }
    public DailyReportStatus Status        { get; private set; } = DailyReportStatus.Draft;
    public string?           GeneratedBy   { get; private set; }
    public DateTime          GeneratedAt   { get; private set; }
    public DateTime?         PublishedAt   { get; private set; }

    public List<MessageGroup> Groups { get; private set; } = [];
    public ParticipantMatrix? Matrix { get; private set; }

    public static DailyReport Generate(
        DateOnly reportDate,
        IReadOnlyList<InterceptionMessage> messages,
        string? generatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var report = new DailyReport
        {
            ReportDate    = reportDate,
            TotalMessages = messages.Count,
            GeneratedBy   = NormalizeOptional(generatedBy),
            GeneratedAt   = DateTimeConverter.Now,
            Status        = DailyReportStatus.Draft
        };

        report.Groups = BuildGroups(report.Id, messages);
        report.Matrix = ParticipantMatrix.Build(report.Id, messages);

        return report;
    }

    public void Publish(string publishedBy)
    {
        if (Status != DailyReportStatus.Draft)
            throw new InvalidOperationException(
                $"Можна публікувати лише Draft-звіт. Поточний статус: {Status}.");
        if (string.IsNullOrWhiteSpace(publishedBy))
            throw new ArgumentException("Ідентифікатор оператора обов'язковий.", nameof(publishedBy));

        Status      = DailyReportStatus.Published;
        PublishedAt = DateTimeConverter.Now;
    }

    internal void Supersede() => Status = DailyReportStatus.Superseded;

    private static List<MessageGroup> BuildGroups(
        Guid reportId,
        IReadOnlyList<InterceptionMessage> messages)
    {
        return [.. messages
            .GroupBy(m => (
                Frequency: m.Frequency   ?? string.Empty,
                Vector:    m.VectorSignal ?? string.Empty,
                Division:  m.Division    ?? string.Empty))
            .Select(g => MessageGroup.Create(
                reportId,
                NormalizeOptional(g.Key.Frequency),
                NormalizeOptional(g.Key.Vector),
                NormalizeOptional(g.Key.Division),
                g.Select(m => m.Id)))];
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
