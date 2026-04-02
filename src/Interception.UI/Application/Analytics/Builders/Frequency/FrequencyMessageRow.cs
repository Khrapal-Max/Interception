//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Builders.Frequency;

public sealed record FrequencyMessageRow(
        Guid Id,
        string Frequency,
        string? Division,
        DateTime ObservedDate);