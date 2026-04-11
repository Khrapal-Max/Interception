//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Common.Events;

namespace Interception.UI.Application.Analytics.Events;

/// <summary>
/// Подія зміни стану/складу групи кандидатів.
/// </summary>
public sealed record ParticipantCandidateGroupChangedIntegrationEvent(
    Guid GroupId,
    ParticipantCandidateGroupChangeType ChangeType,
    DateTime OccurredAtUtc) : IIntegrationEvent;

public enum ParticipantCandidateGroupChangeType
{
    Created = 1,
    Enriched = 2,
    Confirmed = 3,
    Dismissed = 4
}
