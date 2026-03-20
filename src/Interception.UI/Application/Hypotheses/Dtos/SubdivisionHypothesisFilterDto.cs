//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Registry filter for subdivision hypotheses.
/// </summary>
public sealed class SubdivisionHypothesisFilterDto
{
    public string? Query { get; init; }
    public bool IncludeArchived { get; init; }
    public bool OnlyResolved { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 25;
}
