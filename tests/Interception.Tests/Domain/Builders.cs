//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Domain;

namespace Interception.Tests.Domain;

// ---------------------------------------------------------------------------
// InterceptionActionBuilder
// ---------------------------------------------------------------------------

/// <summary>
/// Builder для InterceptionAction в тестах без використання рефлексії.
/// </summary>
internal sealed class InterceptionActionBuilder
{
    private string _name = "Test Action";
    private string _description = "Test Description";

    public InterceptionActionBuilder WithName(string name) { _name = name; return this; }
    public InterceptionActionBuilder WithDescription(string d) { _description = d; return this; }

    public InterceptionAction Build()
        => InterceptionAction.Create(_name, _description);
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

// ---------------------------------------------------------------------------
// ResolvedParticipantBuilder
// ---------------------------------------------------------------------------

/// <summary>
/// Builder для ResolvedParticipant в тестах.
/// </summary>
internal sealed class ResolvedParticipantBuilder
{
    private string _name = "ШАПКА";
    private string _confirmedBy = "operator";
    private string? _role = null;
    private string? _division = null;

    public ResolvedParticipantBuilder WithName(string name) { _name = name; return this; }
    public ResolvedParticipantBuilder WithConfirmedBy(string op) { _confirmedBy = op; return this; }
    public ResolvedParticipantBuilder WithRole(string? role) { _role = role; return this; }
    public ResolvedParticipantBuilder WithDivision(string? division) { _division = division; return this; }

    public ResolvedParticipant Build() =>
        ResolvedParticipant.Create(_name, _confirmedBy, _role, _division);
}
