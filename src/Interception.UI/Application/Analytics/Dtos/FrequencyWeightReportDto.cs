//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Загальний звіт по частотах та вагам підрозділів.
/// </summary>
public sealed record FrequencyWeightReportDto(
    IReadOnlyList<FrequencyWeightDto> Frequencies);
