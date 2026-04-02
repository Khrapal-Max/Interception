//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Exports.Dtos;

/// <summary>
/// Результат формування файла експорту.
/// </summary>
public sealed record ExportFileDto(
    string FileName,
    string ContentType,
    byte[] Content);
