//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Extensions;

internal static class TopologySnapshotPeriodNormalizer
{
    public static (DateTime? DateFromUtc, DateTime? DateToUtc) Normalize(DateTime? dateFromUtc, DateTime? dateToUtc)
    {
        var normalizedFrom = dateFromUtc;
        var normalizedTo = dateToUtc;

        if (normalizedTo.HasValue
            && normalizedTo.Value.TimeOfDay == TimeSpan.Zero
            && normalizedTo.Value > DateTime.MinValue)
        {
            normalizedTo = normalizedTo.Value.AddTicks(-1);
        }

        return (normalizedFrom, normalizedTo);
    }
}
