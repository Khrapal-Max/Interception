//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Interceptions.Services.TextBlock;

/// <summary>
/// Результат парсингу текстового блоку перехоплення.
/// Заповнюється автоматично — оператор лише перевіряє і вводить дію.
/// </summary>
public sealed class TextBlockParseResult
{
    /// <summary>True якщо парсинг пройшов без критичних помилок.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Опис помилки якщо IsSuccess = false.</summary>
    public string? Error { get; init; }

    public DateTime? ObservedDate { get; init; }
    public string? Frequency { get; init; }
    public string? Division { get; init; }
    public string? VectorSignal { get; init; }

    /// <summary>Позивний ініціатора. Null = невідомий.</summary>
    public string? Initiator { get; init; }

    /// <summary>Позивні відповідачів (може бути кілька).</summary>
    public IReadOnlyList<string?> Responders { get; init; } = [];

    /// <summary>Текст діалогу / нотатка.</summary>
    public string? Note { get; init; }
}
