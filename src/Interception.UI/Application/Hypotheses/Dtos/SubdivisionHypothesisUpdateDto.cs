//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Hypotheses.Dtos;

/// <summary>
/// Request for updating subdivision hypothesis metadata.
/// </summary>
public sealed record SubdivisionHypothesisUpdateDto(
    string LabelRaw,
    string? LayerHint,
    string? RmHint,
    string? Note);
