//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Analytics.Dtos;

/// <summary>Один учасник у формі.</summary>
public sealed class ParticipantFormDto
{
    public int Ordinal { get; set; }

    /// <summary>
    /// Ім'я/позивний. null або порожнє → IsUnknown = true.
    /// </summary>
    public string? Name { get; set; }

    public string? Role { get; set; }
    public bool IsUnknown { get; set; }
}
