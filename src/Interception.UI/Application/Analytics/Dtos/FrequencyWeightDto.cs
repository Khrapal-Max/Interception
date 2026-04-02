//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Один блок частоти у ваговому звіті.
/// </summary>
public sealed record FrequencyWeightDto(
    string Frequency,
    string? FrequencyDivision,
    int PersonsCount,
    IReadOnlyList<FrequencyWeightGroupDto> Groups);
