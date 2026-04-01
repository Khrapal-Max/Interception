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

using Interception.Application.Analytics.Dtos;

namespace Interception.Application.Registry.Abstractions;

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
    /// Оновлює порожні або службові значення підрозділу на вибране оператором значення.
    /// </summary>
    Task CorrectDivisionAsync(
        string frequency,
        string division,
        CancellationToken ct = default);
}
