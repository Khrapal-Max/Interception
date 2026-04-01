//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.Domain.Entities;
using Interception.Domain.Records;

namespace Interception.Application.Analytics.Builders;

/// <summary>
/// Описує набір змін, які треба застосувати до open-групи після аналізу.
/// </summary>
internal sealed record OpenGroupUpdate(
    Guid GroupId,
    IReadOnlyList<ParticipantRef> NewRefs,
    double Score,
    PatternMatchReasons Reasons,
    string? SuggestedRole,
    string? SuggestedDivision);
