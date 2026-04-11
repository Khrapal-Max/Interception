//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Entities;

namespace Interception.UI.Application.Analytics.Builders.LinkMap;

internal sealed record MessageRow(
        InterceptionMessage Message,
        string? EffectiveDivision,
        IReadOnlyList<ParticipantSnapshot> KnownParticipants);
