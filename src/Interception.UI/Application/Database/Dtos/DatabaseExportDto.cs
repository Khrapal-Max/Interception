//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Database.Dtos;

/// <summary>
/// Дані підготовленого export-файлу бази.
/// </summary>
public sealed record DatabaseExportDto(
    string FilePath,
    string DownloadFileName,
    DateTime CreatedAtUtc);
