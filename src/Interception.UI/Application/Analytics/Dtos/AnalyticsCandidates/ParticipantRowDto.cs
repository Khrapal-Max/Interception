//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;

public sealed record ParticipantRowDto(
        Guid Id,
        Guid ObservationId,
        int Ordinal,
        string? LabelRaw,
        string? LabelNorm,
        bool IsUnknown,
        string? RoleRaw);