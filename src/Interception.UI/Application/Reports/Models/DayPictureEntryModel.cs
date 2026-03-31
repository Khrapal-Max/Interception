//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Reports.Models;

/// <summary>
/// Один запис картини дня.
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
