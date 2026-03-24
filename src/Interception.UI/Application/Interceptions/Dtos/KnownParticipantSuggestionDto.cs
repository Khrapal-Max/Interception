//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Dtos;

/// <summary>
/// Відомий учасник що за ознаками схожий на НВ з групи кандидатів.
/// Показується в дравері як підказка оператору.
/// </summary>
public sealed class KnownParticipantSuggestionDto
{
    /// <summary>Позивний відомого учасника.</summary>
    public string Name { get; init; } = default!;

    /// <summary>Остання відома роль.</summary>
    public string? Role { get; init; }

    /// <summary>Зважений score збігу [0.0 – 1.0].</summary>
    public double MatchScore { get; init; }

    /// <summary>Які ознаки збіглись.</summary>
    public KnownSuggestionReasonsDto Reasons { get; init; } = new();

    /// <summary>Спільні мітки — ключові слова що зустрічались разом.</summary>
    public IReadOnlyList<string> CommonLabels { get; init; } = [];

    /// <summary>Скільки разів зустрічався в повідомленнях з тими самими ознаками.</summary>
    public int SeenCount { get; init; }
}

/// <summary>Які ознаки збіглись між НВ-групою і відомим учасником.</summary>
public sealed class KnownSuggestionReasonsDto
{
    public bool SameFrequency { get; init; }
    public bool SameVector { get; init; }
    public bool SameDivision { get; init; }
    public bool CloseInTime { get; init; }
    public bool SharedLabels { get; init; }

    public IReadOnlyList<string> ActiveReasons
    {
        get
        {
            var list = new List<string>();
            if (SameFrequency) list.Add("однакова частота");
            if (SameVector) list.Add("однаковий вектор");
            if (SameDivision) list.Add("однаковий підрозділ");
            if (CloseInTime) list.Add("близько в часі");
            if (SharedLabels) list.Add("спільні мітки");
            return list;
        }
    }
}
