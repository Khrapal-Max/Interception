//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Import.Dtos;

namespace Interception.UI.Application.Import.Abstractions;

/// <summary>Контракт сервісу імпорту з Excel.</summary>
public interface IInterceptionImportService
{
    Task<ImportResultDto> ImportAsync(
        Stream stream, string operatorName, CancellationToken cancellationToken = default);
}
