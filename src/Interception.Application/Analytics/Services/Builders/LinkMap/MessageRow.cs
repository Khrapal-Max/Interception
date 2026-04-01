//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Domain.Entities;

namespace Interception.Application.Analytics.Services.Builders.LinkMap;

internal sealed record MessageRow(
        InterceptionMessage Message,
        string? EffectiveDivision,
        IReadOnlyList<ParticipantSnapshot> KnownParticipants);
