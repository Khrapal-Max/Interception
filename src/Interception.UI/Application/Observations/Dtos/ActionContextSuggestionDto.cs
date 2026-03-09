//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Короткий контекстний зв'язок для дії та вже обраних учасників.
/// </summary>
/// <param name="Display">Назва пов'язаної особи.</param>
/// <param name="RelationType">Тип зв'язку: direct / same-action / same-district.</param>
/// <param name="Description">Людський опис зв'язку.</param>
/// <param name="Weight">Сила зв'язку.</param>
/// <param name="LastSeenDate">Остання дата появи такого зв'язку.</param>
public sealed record ActionContextSuggestionDto(
    string Display,
    string RelationType,
    string Description,
    int Weight,
    DateOnly? LastSeenDate);
