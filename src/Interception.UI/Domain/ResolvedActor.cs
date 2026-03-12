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
    /// Підтверджена роль особи.
    /// </summary>
    public string? Role { get; private set; }

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

    /// <summary>
    /// Створює встановлену особу для аналітичної резолюції.
    /// </summary>
    public static ResolvedActor Create(
        Guid id,
        string kind,
        string displayName,
        string? role,
        string? callsign,
        string? note,
        DateTime createdAtUtc,
        string? createdBy)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Actor id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("Actor kind is required.", nameof(kind));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Actor display name is required.", nameof(displayName));

        return new ResolvedActor
        {
            Id = id,
            Kind = kind.Trim(),
            DisplayName = displayName.Trim(),
            Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim(),
            Callsign = string.IsNullOrWhiteSpace(callsign) ? null : callsign.Trim(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAtUtc = createdAtUtc,
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy.Trim()
        };
    }

    public void SetRole(string? role)
    {
        Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
    }
}