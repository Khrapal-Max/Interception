//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Exports.Dtos;

namespace Interception.UI.Application.Exports.Abstractions;

/// <summary>
/// Центральний сервіс Excel-експорту для реєстрів, звітів і аналітики.
/// </summary>
public interface IExcelExportService
{
    /// <summary>
    /// Формує Excel-файл для вибраного типу експорту.
    /// </summary>
    Task<ExportFileDto> ExportAsync(ExportRequestDto request, CancellationToken ct = default);
}
