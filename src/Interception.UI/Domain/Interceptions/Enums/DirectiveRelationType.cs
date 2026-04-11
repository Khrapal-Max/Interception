//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Interceptions.Enums;

/// <summary>
/// Тип структурного зв'язку керування між канонічними особами.
/// </summary>
public enum DirectiveRelationType
{
    Command = 1,
    ReportUp = 2,
    Control = 3,
    Correction = 4,
    Coordination = 5,
    Other = 6
}
