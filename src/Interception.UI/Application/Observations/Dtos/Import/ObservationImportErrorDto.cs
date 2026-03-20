//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos.Import;

/// <summary>
/// Import validation/error row.
/// </summary>
public sealed record ObservationImportErrorDto(
    int RowNumber,
    string Message);
