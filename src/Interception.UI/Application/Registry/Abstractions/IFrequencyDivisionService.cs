//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Registry.Abstractions;

/// <summary>
/// Сервіс реєстру частот і закріплених за ними підрозділів.
/// </summary>
public interface IFrequencyDivisionService
{
    /// <summary>
    /// Повертає список частот з найбільш ймовірним підрозділом для кожної частоти.
    /// </summary>
    Task<IReadOnlyList<FrequencySuggestionDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Коригує підрозділ для вказаної частоти.
    /// Оновлює підрозділ для всіх записів на цій частоті.
    /// </summary>
    Task CorrectDivisionAsync(
        string frequency,
        string division,
        CancellationToken ct = default);
}
