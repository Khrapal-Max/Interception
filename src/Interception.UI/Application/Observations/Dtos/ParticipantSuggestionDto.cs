//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Підказка для вибору учасника під час ручного створення спостереження.
/// </summary>
/// <param name="Key">Стабільний ключ ефективної ідентичності.</param>
/// <param name="Display">Текст, який показується оператору.</param>
/// <param name="Kind">Тип підказки: known / actor / cluster.</param>
/// <param name="Subtitle">Додатковий опис для списку підказок.</param>
/// <param name="ObservationsCount">Кількість спостережень, де зустрічалась особа.</param>
/// <param name="LastSeenDate">Дата останньої появи.</param>
/// <param name="PrimaryRole">Найтиповіша роль для цієї особи.</param>
public sealed record ParticipantSuggestionDto(
    string Key,
    string Display,
    string Kind,
    string? Subtitle,
    int ObservationsCount,
    DateOnly? LastSeenDate,
    string? PrimaryRole);
