//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Models.PatternRecognition;

/// <summary>
/// Мутуючий builder для складання <see cref="ContextProfile"/> із багатьох observation.
/// </summary>
internal sealed class ContextProfileBuilder(string division)
{
    /// <summary>Нормалізоване людиночитне ім'я підрозділу.</summary>
    public string Division { get; } = division;

    /// <summary>Observation-знімки цього контексту.</summary>
    public List<GroupObservationSnapshot> Observations { get; } = [];

    /// <summary>Відомі імена, що регулярно з'являлись у цьому контексті.</summary>
    public HashSet<string> RelatedKnownNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Підтверджені особи, які вже були встановлені в цьому контексті.</summary>
    public HashSet<string> RelatedResolvedNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Кількість confirmed groups, які підкріплюють цей контекст.</summary>
    public int ConfirmedGroupCount { get; set; }

    /// <summary>
    /// Перетворює накопичений стан у завершений profile.
    /// </summary>
    public ContextProfile Build(GroupProfile pivot) => new(
        Division,
        pivot,
        RelatedKnownNames,
        RelatedResolvedNames,
        Observations.Count,
        ConfirmedGroupCount);
}
