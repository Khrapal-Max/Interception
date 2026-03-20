//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Dtos.Import;

namespace Interception.UI.Application.Observations.Abstractions;

/// <summary>
/// Batch import service for raw observations.
/// Parsing stays outside; this service validates, normalizes and persists rows.
/// </summary>
public interface IObservationImportService
{
    Task<ObservationImportResultDto> ImportAsync(
        IReadOnlyCollection<ObservationImportRowDto> rows,
        string source,
        Guid? sourceFileId,
        string? createdBy,
        CancellationToken ct);
}
