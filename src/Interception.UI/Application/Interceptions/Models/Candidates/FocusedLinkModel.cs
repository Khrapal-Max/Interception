//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Models.Candidates;

namespace Interception.UI.Components.Pages.Analytics.LinkMap.Models;

internal sealed record FocusedLinkModel(
    LinkMapGroupModel TargetGroup,
    LinkMapBridgeModel Bridge);
