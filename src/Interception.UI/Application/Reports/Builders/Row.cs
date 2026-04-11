//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Interceptions;

namespace Interception.UI.Application.Reports.Builders;

internal sealed record Row(
        InterceptionMessage Message,
        string? EffectiveDivision,
        HashSet<string> ParticipantSet);
