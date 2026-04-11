//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Interceptions;

public class InterceptionMessageLabel
{
    // FIX: Id ініціалізується тільки тут — прибрали дублювання в Create()
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string NameLabel { get; private set; } = default!;

    public static InterceptionMessageLabel Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва мітки обов'язкова.", nameof(name));

        var label = new InterceptionMessageLabel
        {
            NameLabel = name.Trim()
        };

        return label;
    }
}
