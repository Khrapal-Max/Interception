//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Result of creating/updating observation.
/// </summary>
public sealed record ObservationSaveResultDto(
    Guid ObservationId,
    bool IsDuplicate,
    string ContentHash);
