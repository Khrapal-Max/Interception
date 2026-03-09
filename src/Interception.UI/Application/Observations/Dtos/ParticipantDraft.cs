//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

namespace Interception.UI.Application.Observations.Dtos;

/// <summary>
/// Чернетка рядка учасника у формі ручного створення спостереження.
/// </summary>
public sealed class ParticipantDraft
{
    /// <summary>
    /// Сирий ярлик, який буде збережено в raw snapshot.
    /// </summary>
    public string? LabelRaw { get; set; }

    /// <summary>
    /// Ознака невизначеної особи.
    /// </summary>
    public bool IsUnknown { get; set; }

    /// <summary>
    /// Роль учасника у конкретному raw snapshot.
    /// </summary>
    public string? RoleRaw { get; set; }

    /// <summary>
    /// Ключ вибраної підказки. Дає змогу будувати контекст без зміни raw snapshot.
    /// </summary>
    public string? SelectedKey { get; set; }

    /// <summary>
    /// Текстовий статус для UI: відома особа, раніше фіксувалась тощо.
    /// </summary>
    public string? StatusText { get; set; }
}
