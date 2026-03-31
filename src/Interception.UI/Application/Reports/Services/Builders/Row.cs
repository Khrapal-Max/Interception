//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;

namespace Interception.UI.Application.Reports.Services;

public sealed partial class DayPictureService
{
    internal sealed record Row(
        InterceptionMessage Message,
        string? EffectiveDivision,
        HashSet<string> ParticipantSet);
}
