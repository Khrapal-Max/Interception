//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Dtos;

public sealed record SubdivisionActorStatDto(
    string DisplayName,
    int Count,
    bool IsResolved,
    string Source);
