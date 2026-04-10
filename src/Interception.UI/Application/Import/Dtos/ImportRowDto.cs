//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Import.Dtos;

/// <summary>
/// Один рядок з аркуша «Спостереження», сумісного з поточним Excel-експортом.
/// </summary>
public sealed class ImportRowDto
{
    public int RowNumber { get; init; }
    public DateTime ObservedAtLocal { get; init; }
    public string? Frequency { get; init; }
    public string? Division { get; init; }
    public string? PointSignal { get; init; }
    public string? VectorSignal { get; init; }
    public string? ActionName { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<ImportParticipantDto> Participants { get; init; } = [];
    public IReadOnlyList<string> Labels { get; init; } = [];
}
