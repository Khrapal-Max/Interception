//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Domain.ValueObjects;

/// <summary>
/// Одна клітинка матриці: пара учасників + кількість спільних повідомлень.
/// ParticipantA завжди лексикографічно менший за ParticipantB.
/// </summary>
public sealed record MatrixCell(
    Guid MatrixId,
    string ParticipantA,
    string ParticipantB,
    int InteractionCount);
