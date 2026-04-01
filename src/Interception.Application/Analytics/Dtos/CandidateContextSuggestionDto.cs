//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.Application.Analytics.Dtos;

/// <summary>
/// Ймовірна належність / контекст для НВ-групи.
/// Це не конкретна особа, а середовище в якому група стабільно з'являється:
/// підрозділ, мітки, роль, вже підтверджені особи цього контексту.
/// </summary>
public sealed class CandidateContextSuggestionDto
{
    /// <summary>Назва ймовірного підрозділу / контексту.</summary>
    public string Division { get; init; } = default!;

    /// <summary>Найтиповіша роль у цьому контексті.</summary>
    public string? SuggestedRole { get; init; }

    /// <summary>Зважений score збігу [0.0 – 1.0].</summary>
    public double MatchScore { get; init; }

    /// <summary>Причини чому саме цей контекст підсвічено.</summary>
    public CandidateContextReasonsDto Reasons { get; init; } = new();

    /// <summary>Спільні мітки між НВ-групою і цим контекстом.</summary>
    public IReadOnlyList<string> CommonLabels { get; init; } = [];

    /// <summary>Відомі учасники що часто з'являлись у цьому контексті.</summary>
    public IReadOnlyList<string> RelatedKnownNames { get; init; } = [];

    /// <summary>Підтверджені особи, які вже були впізнані в цьому ж контексті.</summary>
    public IReadOnlyList<string> RelatedResolvedNames { get; init; } = [];

    /// <summary>Скільки повідомлень лягло в основу цього контексту.</summary>
    public int SeenCount { get; init; }

    /// <summary>Скільки confirmed groups вже підтвердили цей контекст.</summary>
    public int ConfirmedGroupCount { get; init; }
}

public sealed class CandidateContextReasonsDto
{
    public bool SameFrequency { get; init; }
    public bool SameVector { get; init; }
    public bool SameDivision { get; init; }
    public bool SameRole { get; init; }
    public bool SharedLabels { get; init; }
    public bool HasConfirmedContext { get; init; }
    public bool PivotIntersection { get; init; }

    public int MatchCount =>
        (SameFrequency ? 1 : 0) +
        (SameVector ? 1 : 0) +
        (SameDivision ? 1 : 0) +
        (SameRole ? 1 : 0) +
        (SharedLabels ? 1 : 0) +
        (HasConfirmedContext ? 1 : 0) +
        (PivotIntersection ? 1 : 0);

    public IReadOnlyList<string> ActiveReasons
    {
        get
        {
            var list = new List<string>();
            if (SameFrequency) list.Add("частота");
            if (SameVector) list.Add("вектор");
            if (SameDivision) list.Add("підрозділ");
            if (SameRole) list.Add("роль");
            if (SharedLabels) list.Add("мітки");
            if (HasConfirmedContext) list.Add("є підтверджені");
            if (PivotIntersection) list.Add("сталі перехрестя");
            return list;
        }
    }
}
