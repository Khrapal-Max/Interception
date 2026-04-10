//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Dtos;

/// <summary>
/// Рядок списку зв'язків структурного керування.
/// </summary>
public sealed class PersonDirectiveRelationListItemDto
{
    public Guid Id { get; init; }
    public Guid? FromCanonicalPersonId { get; init; }
    public Guid? FromResolvedParticipantId { get; init; }
    public string FromDisplayName { get; init; } = string.Empty;
    public Guid? ToCanonicalPersonId { get; init; }
    public Guid? ToResolvedParticipantId { get; init; }
    public string ToDisplayName { get; init; } = string.Empty;
    public DirectiveRelationTypeDto RelationType { get; init; }
    public DirectiveRelationConfidenceDto Confidence { get; init; }
    public Guid? SourceObservationId { get; init; }
    public bool IsManual { get; init; }
    public string? Comment { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
