//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Import.Dtos;

/// <summary>
/// Один учасник з Excel-рядка аркуша «Спостереження».
/// </summary>
public sealed class ImportParticipantDto
{
    public int Ordinal { get; init; }
    public string? Name { get; init; }
    public string? Role { get; init; }
    public bool IsUnknown { get; init; }
}
