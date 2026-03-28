//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Models.Reports;

/// <summary>
/// Група пов'язаних спостережень у межах дня.
/// </summary>
public sealed record DayPictureGroupModel(
    string? GroupKey,
    string? Frequency,
    string? VectorSignal,
    string? Division,
    int MessageCount,
    IReadOnlyList<DayPictureEntryModel> Entries);
