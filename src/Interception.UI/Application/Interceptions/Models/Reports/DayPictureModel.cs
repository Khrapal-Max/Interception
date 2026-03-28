//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Models.Reports;

/// <summary>
/// Коренева модель картини дня.
/// </summary>
public sealed record DayPictureModel(
    DateOnly Day,
    int TotalMessages,
    IReadOnlyList<DayPictureGroupModel> Groups);

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

/// <summary>
/// Один рядок спостереження в межах картини дня.
/// </summary>
public sealed record DayPictureEntryModel(
    Guid MessageId,
    DateTime ObservedDate,
    string? Frequency,
    string? Division,
    string? VectorSignal,
    string? ActionName,
    string? Note,
    IReadOnlyList<string> Participants);
