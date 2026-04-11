//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Entities;

/// <summary>
/// Довідникова роль учасника/особи.
/// Унікальна по назві і використовується для нормалізації ролей у системі.
/// </summary>
public sealed class ParticipantRole
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = default!;

    public string Description { get; private set; } = string.Empty;

    public static ParticipantRole Create(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва ролі обов'язкова.", nameof(name));

        return new ParticipantRole
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty
        };
    }

    public void Update(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва ролі обов'язкова.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
    }
}
