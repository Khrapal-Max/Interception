//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Enums;

namespace Interception.UI.Application.Interceptions.Dtos;

/// <summary>
/// Дані збереження простого ручного зв'язку керування.
/// На кожному боці може бути або канонічна особа, або окремий підтверджений запис.
/// </summary>
public sealed class PersonDirectiveRelationSaveDto
{
    public Guid? FromCanonicalPersonId { get; set; }
    public Guid? FromResolvedParticipantId { get; set; }
    public Guid? ToCanonicalPersonId { get; set; }
    public Guid? ToResolvedParticipantId { get; set; }
    public DirectiveRelationType RelationType { get; set; } = DirectiveRelationType.Command;
    public DirectiveRelationConfidence Confidence { get; set; } = DirectiveRelationConfidence.High;
    public Guid? SourceObservationId { get; set; }
    public bool IsManual { get; set; } = true;
    public string? Comment { get; set; }
}
