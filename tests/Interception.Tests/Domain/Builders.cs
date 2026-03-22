//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;

namespace Interception.Tests.Domain;

// ---------------------------------------------------------------------------
// InterceptionActionBuilder
// ---------------------------------------------------------------------------

/// <summary>
/// Builder для InterceptionAction в тестах.
/// InterceptionAction не має публічного конструктора з параметрами —
/// використовуємо рефлексію для встановлення приватних властивостей.
/// </summary>
internal sealed class InterceptionActionBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Action";
    private string _description = "Test Description";

    public InterceptionActionBuilder WithId(Guid id) { _id = id; return this; }
    public InterceptionActionBuilder WithName(string name) { _name = name; return this; }
    public InterceptionActionBuilder WithDescription(string d) { _description = d; return this; }

    public InterceptionAction Build()
    {
        var action = new InterceptionAction();
        SetPrivate(action, nameof(InterceptionAction.Id), _id);
        SetPrivate(action, nameof(InterceptionAction.Name), _name);
        SetPrivate(action, nameof(InterceptionAction.Description), _description);
        return action;
    }

    private static void SetPrivate<T>(object obj, string propName, T value)
    {
        var prop = obj.GetType().GetProperty(propName)
            ?? throw new InvalidOperationException($"Property '{propName}' not found.");
        prop.SetValue(obj, value);
    }
}

// ---------------------------------------------------------------------------
// InterceptionMessageParticipantBuilder
// ---------------------------------------------------------------------------

/// <summary>
/// Builder для InterceptionMessageParticipant.
/// Використовує internal конструктор безпосередньо —
/// тестовий проєкт має бути в InternalsVisibleTo.
/// </summary>
internal sealed class InterceptionMessageParticipantBuilder
{
    private Guid _messageId = Guid.NewGuid();
    private string? _name = "TestParticipant";
    private bool _isUnknown = false;
    private string? _role = null;
    private int _ordinal = 1;

    public InterceptionMessageParticipantBuilder WithMessageId(Guid id) { _messageId = id; return this; }
    public InterceptionMessageParticipantBuilder WithName(string? name) { _name = name; return this; }
    public InterceptionMessageParticipantBuilder WithIsUnknown(bool unknown) { _isUnknown = unknown; return this; }
    public InterceptionMessageParticipantBuilder WithRole(string? role) { _role = role; return this; }
    public InterceptionMessageParticipantBuilder WithOrdinal(int ordinal) { _ordinal = ordinal; return this; }

    public InterceptionMessageParticipant Build() =>
        new(_messageId, _name, _isUnknown, _role, _ordinal);
}
