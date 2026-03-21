//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Dtos;

/// <summary>
/// Один рядок з Excel-файлу, розпарсений у типізований DTO.
/// Відповідає колонкам: Дата | Час | Частота | Р/М | Точка перехвату |
///   Вектор сігнала | Ініціатор | Роль ініціатора | Підрозділ ініціатора |
///   Відповідач | Роль відповідача | Дія | Деталі
/// </summary>
public sealed class ImportRowDto
{
    public int RowNumber { get; init; }

    public DateOnly Date { get; init; }
    public TimeOnly Time { get; init; }

    /// <summary>Частота сигналу, наприклад "157.0250".</summary>
    public string? Frequency { get; init; }

    /// <summary>Р/М — підрозділ (Division).</summary>
    public string? Division { get; init; }

    /// <summary>Точка перехвату (PointSignal).</summary>
    public string? PointSignal { get; init; }

    /// <summary>Вектор сігнала, наприклад "степове-олексіївка".</summary>
    public string? VectorSignal { get; init; }

    /// <summary>Позивний ініціатора. "НВ" або null → невідомий.</summary>
    public string? InitiatorName { get; init; }
    public string? InitiatorRole { get; init; }

    /// <summary>Підрозділ ініціатора (зберігається як Division учасника).</summary>
    public string? InitiatorDivision { get; init; }

    /// <summary>Позивний відповідача. "НВ" або null → невідомий.</summary>
    public string? ResponderName { get; init; }
    public string? ResponderRole { get; init; }

    /// <summary>Назва дії — шукається в довіднику InterceptionAction.</summary>
    public string? ActionName { get; init; }

    /// <summary>Деталі / нотатка.</summary>
    public string? Details { get; init; }
}
