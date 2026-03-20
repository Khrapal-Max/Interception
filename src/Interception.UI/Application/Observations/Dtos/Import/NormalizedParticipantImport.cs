//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos.Import;

public sealed record NormalizedParticipantImport(
       string? LabelRaw,
       bool IsUnknown,
       string? RoleRaw);
