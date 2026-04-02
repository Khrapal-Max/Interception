//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Services;

public sealed partial class FrequencyWeightReportService
{
    public sealed record ResolvedRow(
        Guid Id,
        string Name,
        string? Division);
}