//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Import;

namespace Interception.UI.Application.Interceptions.Abstractions;

/// <summary>Контракт сервісу імпорту з Excel.</summary>
public interface IInterceptionImportService
{
    Task<ImportResult> ImportAsync(
        Stream stream, string operatorName, CancellationToken cancellationToken = default);
}
