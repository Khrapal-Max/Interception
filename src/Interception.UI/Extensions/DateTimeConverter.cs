//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Extensions;

/// <summary>
/// Хелпер для роботи з DateTime на рівні UI.
///
/// Колонки в БД мають тип 'timestamp with time zone' (timestamptz) —
/// Npgsql приймає ТІЛЬКИ Kind=Utc для цього типу.
///
/// Оператор вводить локальний час у форму → конвертуємо в UTC перед збереженням.
/// При відображенні → конвертуємо назад в локальний час.
/// </summary>
public static class DateTimeConverter
{
    /// <summary>
    /// Поточний час у UTC.
    /// Використовується замість DateTime.Now при ініціалізації форм.
    /// Для відображення у datetime-local інпуті — використовуй <see cref="ToDisplay"/>.
    /// </summary>
    public static DateTime Now => DateTime.UtcNow;

    /// <summary>
    /// Парсить рядок з datetime-local інпута (локальний час оператора)
    /// і конвертує в UTC для збереження в БД.
    /// Повертає null якщо рядок не валідний.
    /// </summary>
    public static DateTime? Parse(string? value)
        => DateTime.TryParse(value, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime()
            : null;

    /// <summary>
    /// Конвертує UTC значення з БД в локальний час для відображення у datetime-local інпуті.
    /// </summary>
    public static DateTime ToDisplay(DateTime utcValue)
        => utcValue.Kind == DateTimeKind.Utc
            ? utcValue.ToLocalTime()
            : utcValue;

    /// <summary>
    /// Форматує DateTime для datetime-local інпута (yyyy-MM-ddTHH:mm).
    /// Автоматично конвертує UTC в локальний час.
    /// </summary>
    public static string Format(DateTime utcValue)
        => ToDisplay(utcValue).ToString("yyyy-MM-ddTHH:mm");

    /// <summary>
    /// Конвертує будь-який DateTime в UTC.
    /// Якщо Kind=Unspecified — вважає що це локальний час.
    /// </summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime(),
        _ => value.ToUniversalTime()
    };
}
