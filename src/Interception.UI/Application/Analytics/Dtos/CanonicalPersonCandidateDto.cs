//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Analytics.Dtos;

/// <summary>
/// Кандидат на злиття в канонічну особу.
/// </summary>
public sealed class CanonicalPersonCandidateDto
{
    public string CandidateKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int ConfirmedRowsCount { get; init; }
    public int DistinctFrequencyCount { get; init; }
    public int DistinctDivisionCount { get; init; }
    public bool HasCanonicalPerson { get; init; }
    public Guid? CanonicalPersonId { get; init; }
    public DateTime? LastConfirmedAtUtc { get; init; }
}
