//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.Records;

/// <summary>
/// Value object — запис про одне повідомлення всередині MessageGroup.
/// Окрема таблиця в БД (owned entity або join table).
/// </summary>
public sealed record MessageGroupEntry(Guid MessageGroupId, Guid MessageId);
