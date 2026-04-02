//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Database.Dtos;

namespace Interception.UI.Application.Database.Abstractions;

/// <summary>
/// Сервіс адміністрування portable SQLite-бази даних.
/// </summary>
public interface IDatabaseMaintenanceService
{
    /// <summary>
    /// Повертає поточний стан бази даних та службових папок.
    /// </summary>
    Task<DatabaseStatusDto> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Формує експортний snapshot SQLite-бази та повертає шлях до файлу для завантаження.
    /// </summary>
    Task<DatabaseExportDto> PrepareExportAsync(CancellationToken ct = default);

    /// <summary>
    /// Імпортує SQLite-базу з файлу та замінює поточну portable-базу.
    /// </summary>
    Task<DatabaseOperationResultDto> ImportAsync(Stream stream, string? fileName, string operatorName, CancellationToken ct = default);

    /// <summary>
    /// Очищає поточну базу, заново створює схему та виконує seed довідника.
    /// </summary>
    Task<DatabaseOperationResultDto> ClearAsync(string operatorName, CancellationToken ct = default);
}
