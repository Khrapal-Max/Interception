//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Generic save result for hypothesis create operations.
/// </summary>
public sealed record HypothesisSaveResultDto(Guid Id, bool IsCreated);
