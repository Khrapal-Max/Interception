//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain;

/// <summary>
/// Канонічна сутність, у яку може бути резолвлений unknown-кластер.
/// Історичні raw-записи не змінюються, а лише починають відображатися через цього актора.
/// </summary>
public sealed class ResolvedActor
{
    /// <summary>
    /// Ідентифікатор актора.
    /// </summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// Вид актора: person / organization / device / other.
    /// </summary>
    public string Kind { get; private set; } = default!;

    /// <summary>
    /// Основне відображуване ім'я.
    /// </summary>
    public string DisplayName { get; private set; } = default!;

    /// <summary>
    /// Опціональний позивний або короткий ідентифікатор.
    /// </summary>
    public string? Callsign { get; private set; }

    /// <summary>
    /// Аналітична примітка.
    /// </summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Дата створення актора.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Хто створив запис.
    /// </summary>
    public string? CreatedBy { get; private set; }

    /// <summary>
    /// Кластери, які зараз резолвляться в цього актора.
    /// </summary>
    public List<UnknownCluster> UnknownClusters { get; private set; } = [];

    private ResolvedActor()
    {
    }
}
