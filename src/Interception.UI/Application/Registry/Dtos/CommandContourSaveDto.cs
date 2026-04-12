//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Registry.Dtos;

/// <summary>
/// Дані збереження простого ручного зв'язку керування.
/// На кожному боці може бути або канонічна особа, або окремий підтверджений запис.
/// </summary>
public sealed class CommandContourSaveDto
{
    public Guid? FromCanonicalPersonId { get; set; }
    public Guid? FromResolvedParticipantId { get; set; }
    public Guid? ToCanonicalPersonId { get; set; }
    public Guid? ToResolvedParticipantId { get; set; }
    public CommandContourRelationTypeDto RelationType { get; set; } = CommandContourRelationTypeDto.Command;
    public CommandContourConfidenceDto Confidence { get; set; } = CommandContourConfidenceDto.High;
    public Guid? SourceObservationId { get; set; }
    public bool IsManual { get; set; } = true;
    public string? Comment { get; set; }
}
