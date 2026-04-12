//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Registry.Dtos;

/// <summary>
/// Рядок списку зв'язків структурного керування.
/// </summary>
public sealed class CommandContourListItemDto
{
    public Guid Id { get; init; }
    public Guid? FromCanonicalPersonId { get; init; }
    public Guid? FromResolvedParticipantId { get; init; }
    public string FromDisplayName { get; init; } = string.Empty;
    public Guid? ToCanonicalPersonId { get; init; }
    public Guid? ToResolvedParticipantId { get; init; }
    public string ToDisplayName { get; init; } = string.Empty;
    public CommandContourRelationTypeDto RelationType { get; init; }
    public CommandContourConfidenceDto Confidence { get; init; }
    public Guid? SourceObservationId { get; init; }
    public bool IsManual { get; init; }
    public string? Comment { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
