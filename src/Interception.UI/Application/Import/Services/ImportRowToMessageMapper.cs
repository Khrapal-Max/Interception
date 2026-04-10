//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Import.Dtos;
using Interception.UI.Domain;
using Interception.UI.Domain.ValueObjects;
using Interception.UI.Extensions;

namespace Interception.UI.Application.Import.Services;

/// <summary>
/// Anti-corruption mapper для трансляції Import DTO у доменну модель перехоплення.
/// Відповідає за нормалізацію вхідних значень та формування причинних помилок імпорту.
/// </summary>
internal static class ImportRowToMessageMapper
{
    private const string ErrorPrefix = "IMPORT_MAPPING";

    public static (InterceptionMessage? Message, ImportRowErrorDto? Error) TryMap(
        ImportRowDto row,
        ImportContextCache cache,
        string operatorName)
    {
        var action = cache.FindAction(row.ActionName);
        if (action is null)
            return (null, Fail(row.RowNumber, $"Дію '{row.ActionName}' не знайдено в довіднику."));

        if (row.Participants.Count == 0)
            return (null, Fail(row.RowNumber, "Потрібно щонайменше одного учасника в рядку імпорту."));

        var observedDateUtc = ConverterDateTimeExtensions.ToUtc(row.ObservedAtLocal);
        var frequency = FrequencyCode.Create(row.Frequency)?.Value;
        var division = DivisionName.Create(row.Division)?.Value;

        InterceptionMessage message;
        try
        {
            message = InterceptionMessage.Create(
                observedDate: observedDateUtc,
                frequency: frequency,
                division: division,
                vectorSignal: row.VectorSignal,
                interceptionAction: action,
                note: row.Note,
                createdBy: operatorName,
                pointSignal: row.PointSignal);
        }
        catch (Exception ex)
        {
            return (null, Fail(row.RowNumber, ex.Message));
        }

        foreach (var participant in row.Participants.OrderBy(x => x.Ordinal))
        {
            var normalizedName = PersonName.Create(participant.Name)?.Value;
            if (!participant.IsUnknown && normalizedName is null)
                return (null, Fail(row.RowNumber, $"Учасник #{participant.Ordinal} має некоректне ім'я."));

            try
            {
                message.AddParticipant(
                    normalizedName,
                    participant.IsUnknown,
                    cache.ResolveRole(participant.Role),
                    participant.Ordinal);
            }
            catch (Exception ex)
            {
                return (null, Fail(row.RowNumber, $"Учасник #{participant.Ordinal}: {ex.Message}"));
            }
        }

        foreach (var label in row.Labels
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Select(x => x.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                message.AddLabel(label);
            }
            catch (Exception ex)
            {
                return (null, Fail(row.RowNumber, $"Мітка '{label}': {ex.Message}"));
            }
        }

        return (message, null);
    }

    private static ImportRowErrorDto Fail(int rowNumber, string reason)
        => new(rowNumber, $"[{ErrorPrefix}] {reason} Рядок пропущено.");
}
