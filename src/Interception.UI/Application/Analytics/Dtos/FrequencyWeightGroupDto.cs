//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Одна група підрозділу в межах частоти.
/// </summary>
public sealed record FrequencyWeightGroupDto(
    string GroupName,
    int PersonsCount,
    decimal WeightPercent);
