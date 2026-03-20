//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos.Import;

/// <summary>
/// Result of batch observation import.
/// </summary>
public sealed record ObservationImportResultDto(
    int ImportedCount,
    int DuplicateCount,
    int ErrorCount,
    IReadOnlyList<ObservationImportErrorDto> Errors);
