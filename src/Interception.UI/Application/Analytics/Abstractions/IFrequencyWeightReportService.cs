//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Analytics.Abstractions;

/// <summary>
/// Будує простий ваговий звіт по частотах і групах підрозділів.
/// </summary>
public interface IFrequencyWeightReportService
{
    /// <summary>
    /// Формує звіт по всіх частотах з відомими учасниками та їх ваговими групами.
    /// </summary>
    Task<FrequencyWeightReportDto> BuildAsync(CancellationToken ct = default);
}
