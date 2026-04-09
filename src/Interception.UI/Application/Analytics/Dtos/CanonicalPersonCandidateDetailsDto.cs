//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Деталі кандидата на об'єднання в об’єднаний профіль.
/// </summary>
public sealed class CanonicalPersonCandidateDetailsDto
{
    public string CandidateKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool HasCanonicalPerson { get; init; }
    public Guid? CanonicalPersonId { get; init; }
    public string? CanonicalDisplayName { get; init; }
    public string? CanonicalNote { get; init; }
    public string? Warning { get; init; }
    public IReadOnlyList<CanonicalPersonCandidateRowDto> Rows { get; init; } = [];
    public IReadOnlyList<string> Frequencies { get; init; } = [];
    public IReadOnlyList<string> Divisions { get; init; } = [];
}
