//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Dtos;

/// <summary>Скорочений опис учасника для відображення у списку.</summary>
public sealed class ParticipantBriefDto
{
    public string? Name { get; init; }
    public string? Role { get; init; }
    public bool IsUnknown { get; init; }
    public int Ordinal { get; init; }

    public string DisplayName => IsUnknown ? "НВ" : Name ?? "НВ";
}
