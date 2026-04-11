//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Entities;

public class InterceptionAction
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = default!;

    public string Description { get; private set; } = default!;

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    public static InterceptionAction Create(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва дії обов'язкова.", nameof(name));

        return new InterceptionAction
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty
        };
    }

    // -------------------------------------------------------------------------
    // Behaviour
    // -------------------------------------------------------------------------

    public void Update(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва дії обов'язкова.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
    }
}
