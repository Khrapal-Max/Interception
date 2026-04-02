//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Database.Dtos;

/// <summary>
/// Результат операції імпорту або очищення бази.
/// </summary>
public sealed record DatabaseOperationResultDto(
    bool Success,
    string Title,
    string Message,
    string? BackupFilePath = null);
