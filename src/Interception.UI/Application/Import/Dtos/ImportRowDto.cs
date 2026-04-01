//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Import.Dtos;

/// <summary>
/// Один рядок з Excel-файлу, розпарсений у типізований DTO.
/// Відповідає колонкам:
///   Col 1  Дата
///   Col 2  Час
///   Col 3  Частота
///   Col 4  Р/М (Division)
///   Col 5  Точка перехвату
///   Col 6  Вектор сігнала
///   Col 7  Ініціатор
///   Col 8  Роль ініціатора
///   Col 9  Відповідач
///   Col 10 Роль відповідача
///   Col 11 Дія
///   Col 12 Деталі
/// </summary>
public sealed class ImportRowDto
{
    public int RowNumber { get; init; }

    public DateOnly Date { get; init; }
    public TimeOnly Time { get; init; }

    public string? Frequency { get; init; }
    public string? Division { get; init; }
    public string? PointSignal { get; init; }
    public string? VectorSignal { get; init; }

    public string? InitiatorName { get; init; }
    public string? InitiatorRole { get; init; }

    public string? ResponderName { get; init; }
    public string? ResponderRole { get; init; }

    public string? ActionName { get; init; }
    public string? Details { get; init; }
}
