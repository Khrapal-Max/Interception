//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Reports.Dtos;

public sealed record SubdivisionTagStatDto(
    string Value,
    TagKind Kind,
    int Count);
