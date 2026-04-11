//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------


//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain.Interceptions.Enums;

namespace Interception.UI.Domain.Interceptions.Policies;

/// <summary>
/// Політика інтерпретації фактів структурного керування для аналітики ієрархії.
/// </summary>
public static class DirectiveRelationPolicy
{
    /// <summary>
    /// Формує людиночитабельний підпис факту керування для пояснення ребра.
    /// </summary>
    public static string BuildLabel(DirectiveRelationType relationType, DirectiveRelationConfidence confidence)
    {
        var relationLabel = relationType switch
        {
            DirectiveRelationType.Command => "явний наказ",
            DirectiveRelationType.ReportUp => "явний зв'язок керування",
            DirectiveRelationType.Control => "явний контроль",
            DirectiveRelationType.Correction => "явне коригування",
            DirectiveRelationType.Coordination => "явна координація",
            _ => "явний структурний зв'язок"
        };

        var confidenceSuffix = confidence switch
        {
            DirectiveRelationConfidence.High => " (висока впевненість)",
            DirectiveRelationConfidence.Medium => " (середня впевненість)",
            _ => " (потребує підтвердження)"
        };

        return relationLabel + confidenceSuffix;
    }

    /// <summary>
    /// Базова вага за довірою до факту.
    /// </summary>
    public static int GetConfidenceScore(DirectiveRelationConfidence confidence)
        => confidence switch
        {
            DirectiveRelationConfidence.High => 300,
            DirectiveRelationConfidence.Medium => 200,
            _ => 100
        };

    /// <summary>
    /// Базова вага за типом зв'язку.
    /// </summary>
    public static int GetTypeScore(DirectiveRelationType relationType)
        => relationType switch
        {
            DirectiveRelationType.Command => 60,
            DirectiveRelationType.Control => 50,
            DirectiveRelationType.Correction => 45,
            DirectiveRelationType.ReportUp => 35,
            DirectiveRelationType.Coordination => 20,
            _ => 10
        };
}
