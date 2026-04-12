//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Registry.Dtos;

/// <summary>
/// Варіант вибору особи у формі створення зв'язку керування.
/// Це може бути або канонічна особа, або поодинокий підтверджений запис.
/// </summary>
public sealed class PersonDirectiveRelationOptionDto
{
    public Guid IdentityId { get; init; }
    public bool IsCanonicalPerson { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Role { get; init; }
    public string? Division { get; init; }
    public string? Frequency { get; init; }
    public string KindLabel { get; init; } = string.Empty;
}
