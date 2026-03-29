//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Models.Candidates;

/// <summary>
/// Агрегований контекст підрозділу / середовища для non-person suggestions.
/// </summary>
internal sealed record ContextProfile(
    string Division,
    GroupProfile Pivot,
    HashSet<string> RelatedKnownNames,
    HashSet<string> RelatedResolvedNames,
    int SeenCount,
    int ConfirmedGroupCount);
