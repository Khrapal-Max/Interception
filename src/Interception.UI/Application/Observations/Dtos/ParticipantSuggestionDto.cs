//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Suggestion for already seen known participant.
/// </summary>
public sealed record ParticipantSuggestionDto(
    string LabelRaw,
    string LabelNorm,
    string? PrimaryRole,
    int SeenCount,
    DateTime? LastSeenAtUtc);
