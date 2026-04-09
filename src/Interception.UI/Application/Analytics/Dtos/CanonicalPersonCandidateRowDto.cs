//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Підтверджений рядок, який може бути рядком об’єднаного профілю.
/// </summary>
public sealed class CanonicalPersonCandidateRowDto
{
    public Guid ResolvedParticipantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Frequency { get; init; }
    public string? Role { get; init; }
    public string? Division { get; init; }
    public DateTime ConfirmedAtUtc { get; init; }
    public bool IsLinkedToCanonical { get; init; }
    public int ObservationCount { get; init; }
}
