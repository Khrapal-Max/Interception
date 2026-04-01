//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Import.Dtos;

/// <summary>
/// Підсумок операції імпорту — повертається в UI для відображення результату.
/// </summary>
public sealed class ImportResultDto
{
    /// <summary>Кількість успішно створених InterceptionMessage.</summary>
    public int ImportedCount { get; init; }

    /// <summary>Кількість рядків, які були пропущені через помилки.</summary>
    public int SkippedCount { get; init; }

    /// <summary>Список помилок з номерами рядків.</summary>
    public IReadOnlyList<ImportRowErrorDto> Errors { get; init; } = [];

    public bool HasErrors => Errors.Count > 0;
    public int TotalRows => ImportedCount + SkippedCount;
}

/// <summary>Помилка одного рядка імпорту.</summary>
public sealed record ImportRowErrorDto(int RowNumber, string Message);
