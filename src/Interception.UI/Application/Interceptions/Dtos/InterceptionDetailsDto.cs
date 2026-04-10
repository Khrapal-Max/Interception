//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Dtos;

/// <summary>
/// Детальна read-model картка перехоплення для режиму редагування.
/// </summary>
public sealed class InterceptionDetailsDto
{
    public Guid Id { get; init; }
    public DateTime ObservedDate { get; init; }
    public string? Frequency { get; init; }
    public string? Division { get; init; }
    public string? PointSignal { get; init; }
    public string? VectorSignal { get; init; }
    public Guid? InterceptionActionId { get; init; }
    public string? Note { get; init; }
    public List<InterceptionDetailsParticipantDto> Participants { get; init; } = [];
    public List<string> Labels { get; init; } = [];
}

public sealed class InterceptionDetailsParticipantDto
{
    public int Ordinal { get; init; }
    public string? Name { get; init; }
    public string? Role { get; init; }
    public bool IsUnknown { get; init; }
}
