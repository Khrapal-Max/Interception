//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Request for resolving a person hypothesis into a confirmed actor.
/// </summary>
public sealed record ResolvedActorUpsertDto(
    string DisplayName,
    string? PrimaryRole,
    string? Note);
