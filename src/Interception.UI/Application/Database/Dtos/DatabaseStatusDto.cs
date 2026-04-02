//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Database.Dtos;

/// <summary>
/// Поточний стан portable-бази та службових файлів.
/// </summary>
public sealed record DatabaseStatusDto(
    string DatabasePath,
    bool DatabaseExists,
    long DatabaseSizeBytes,
    DateTime? DatabaseLastWriteUtc,
    bool KeysDirectoryExists,
    string KeysDirectoryPath,
    int ActionsCount,
    int MessagesCount,
    int ResolvedParticipantsCount,
    bool HasWalFile,
    bool HasShmFile);
