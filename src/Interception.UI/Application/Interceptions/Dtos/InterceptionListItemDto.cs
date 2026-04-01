//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;

namespace Interception.UI.Application.Interceptions.Dtos;

// ---------------------------------------------------------------------------
// Список (реєстр)
// ---------------------------------------------------------------------------

/// <summary>Рядок у реєстрі спостережень.</summary>
public sealed class InterceptionListItemDto
{
    public Guid Id { get; init; }
    public DateTime ObservedDate { get; init; }
    public string? Frequency { get; init; }
    public string? VectorSignal { get; init; }
    public string? Division { get; init; }
    public string? ActionName { get; init; }
    public List<ParticipantBriefDto> Participants { get; init; } = [];
    public List<string> Labels { get; init; } = [];
}
