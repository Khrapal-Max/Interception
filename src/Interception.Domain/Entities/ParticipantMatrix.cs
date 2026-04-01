//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Domain.Records;

namespace Interception.Domain.Entities;

/// <summary>
/// Матриця взаємодій між учасниками в межах одного DailyReport.
///
/// Кожен запис MatrixCell показує, скільки разів учасник A та учасник B
/// фігурували в одному й тому ж перехопленому повідомленні протягом дня.
/// Це дозволяє швидко виявити найактивніші пари / мережі учасників.
///
/// Матриця симетрична: (A, B) та (B, A) — це один і той самий запис,
/// де ParticipantA завжди < ParticipantB (лексикографічно) для уникнення дублів.
/// </summary>
public class ParticipantMatrix
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>FK до DailyReport.</summary>
    public Guid DailyReportId { get; private set; }
    public DailyReport DailyReport { get; private set; } = default!;

    public List<MatrixCell> Cells { get; private set; } = [];

    // -----------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------

    public static ParticipantMatrix Build(
        Guid dailyReportId,
        IEnumerable<InterceptionMessage> messages)
    {
        var matrix = new ParticipantMatrix { DailyReportId = dailyReportId };

        // Словник: (normA, normB) → кількість спільних повідомлень
        var counts = new Dictionary<(string, string), int>();

        foreach (var msg in messages)
        {
            // Беремо лише відомих учасників — невідомі теж враховуються,
            // але під плейсхолдером "Unknown-{Ordinal}"
            var names = msg.Participants
                .Select(p => p.IsUnknown
                    ? $"Unknown-{p.Ordinal}"
                    : p.Name ?? $"Unknown-{p.Ordinal}")
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            // Перебираємо всі пари учасників у цьому повідомленні
            for (var i = 0; i < names.Count; i++)
                for (var j = i + 1; j < names.Count; j++)
                {
                    var key = (names[i], names[j]);   // вже відсортовано → немає дублів
                    counts[key] = counts.GetValueOrDefault(key) + 1;
                }
        }

        // Перетворюємо словник у клітинки матриці
        matrix.Cells = [.. counts
            .Select(kv => new MatrixCell(
                matrix.Id,
                kv.Key.Item1,
                kv.Key.Item2,
                kv.Value))];

        return matrix;
    }

    // -----------------------------------------------------------------
    // Queries
    // -----------------------------------------------------------------

    /// <summary>
    /// Повертає всіх учасників, з якими взаємодіяв заданий учасник,
    /// відсортованих за кількістю взаємодій (спадання).
    /// </summary>
    public IEnumerable<(string Partner, int Count)> GetInteractionsFor(string participantName)
    {
        if (string.IsNullOrWhiteSpace(participantName))
            yield break;

        var norm = participantName.Trim();

        foreach (var cell in Cells.OrderByDescending(c => c.InteractionCount))
        {
            if (string.Equals(cell.ParticipantA, norm, StringComparison.OrdinalIgnoreCase))
                yield return (cell.ParticipantB, cell.InteractionCount);

            else if (string.Equals(cell.ParticipantB, norm, StringComparison.OrdinalIgnoreCase))
                yield return (cell.ParticipantA, cell.InteractionCount);
        }
    }

    /// <summary>
    /// Повертає топ-N найактивніших пар.
    /// </summary>
    public IEnumerable<MatrixCell> GetTopPairs(int count = 10)
        => Cells.OrderByDescending(c => c.InteractionCount).Take(count);
}
