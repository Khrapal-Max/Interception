//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Import;

public sealed record ObservationImportParticipant(
    string? LabelRaw,
    bool IsUnknown,
    string? RoleRaw);
