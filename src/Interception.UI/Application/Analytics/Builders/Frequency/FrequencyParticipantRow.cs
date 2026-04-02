//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Builders.Frequency;

public sealed record FrequencyParticipantRow(
        Guid Id,
        Guid MessageId,
        string? Name,
        bool IsUnknown,
        string? MessageDivision);