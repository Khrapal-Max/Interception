//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Import.Abstractions;

/// <summary>
/// Сервіс повного експорту та імпорту даних БД у portable JSON-форматі.
/// </summary>
public interface IDatabaseImportExportService
{
    /// <summary>
    /// Експортує всі таблиці схеми <c>public</c> у вказаний потік.
    /// </summary>
    Task ExportAsync(Stream output, CancellationToken cancellationToken = default);

    /// <summary>
    /// Імпортує дані з portable JSON та повністю замінює поточний вміст БД.
    /// </summary>
    Task ImportAsync(Stream input, CancellationToken cancellationToken = default);
}
