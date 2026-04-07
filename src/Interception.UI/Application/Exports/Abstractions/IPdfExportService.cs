//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Exports.Dtos;

namespace Interception.UI.Application.Exports.Abstractions;

/// <summary>
/// Центральний сервіс PDF-експорту для звітів і аналітики.
/// </summary>
public interface IPdfExportService
{
    /// <summary>
    /// Формує PDF-файл для вибраного типу експорту.
    /// </summary>
    Task<ExportFileDto> ExportAsync(ExportRequestDto request, CancellationToken ct = default);
}
