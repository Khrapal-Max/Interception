//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Observations;

/// <summary>
/// Сторінка деталізації окремого спостереження.
/// </summary>
public partial class ObservationDetailsPage : ComponentBase, IDisposable
{
    /// <summary>
    /// Ідентифікатор спостереження.
    /// </summary>
    [Parameter] public Guid ObservationId { get; set; }

    private readonly CancellationTokenSource _lifetimeCts = new();

    private ObservationDetailsDto? _details;
    private bool _loading;
    private bool _editDrawerOpen;

    [Inject] private IObservationRegistryService RegistryService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;

    #region Lifecycle

    /// <summary>
    /// Завантажує деталізацію при зміні route-параметра.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    #endregion

    #region Page actions

    /// <summary>
    /// Перезавантажує деталізацію спостереження.
    /// </summary>
    private async Task ReloadAsync()
    {
        await LoadAsync();
    }

    /// <summary>
    /// Повертає користувача до реєстру спостережень.
    /// </summary>
    private void GoBack() => Navigation.NavigateTo("/observations");

    /// <summary>
    /// Відкриває drawer редагування поточного спостереження.
    /// </summary>
    private void OpenEditDrawer()
    {
        if (_details is not null)
            _editDrawerOpen = true;
    }

    #endregion

    #region Drawer callbacks

    /// <summary>
    /// Оновлює стан drawer редагування.
    /// </summary>
    private Task HandleEditDrawerChangedAsync(bool isOpen)
    {
        _editDrawerOpen = isOpen;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Обробляє збереження спостереження після редагування.
    /// </summary>
    private async Task HandleSavedAsync(Guid observationId)
    {
        _editDrawerOpen = false;

        if (observationId != ObservationId)
        {
            Navigation.NavigateTo($"/observations/{observationId}");
            return;
        }

        await LoadAsync();
    }

    #endregion

    #region Loading

    /// <summary>
    /// Завантажує деталізацію вибраного спостереження.
    /// </summary>
    private async Task LoadAsync()
    {
        _loading = true;

        try
        {
            _details = await RegistryService.GetByIdAsync(ObservationId, _lifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ToastService.Error("Помилка завантаження деталізації.", ex.Message);
            _details = null;
        }
        finally
        {
            _loading = false;
        }
    }

    #endregion

    #region Presentation helpers

    /// <summary>
    /// Повертає текст сили зв'язку підрозділу.
    /// </summary>
    private static string GetSubdivisionStrengthText(SubdivisionLinkStrength? value)
        => value switch
        {
            SubdivisionLinkStrength.Weak => "Слабкий",
            SubdivisionLinkStrength.Medium => "Середній",
            SubdivisionLinkStrength.Strong => "Сильний",
            _ => "—"
        };

    /// <summary>
    /// Повертає текст джерела визначення підрозділу.
    /// </summary>
    private static string GetSubdivisionSourceText(ObservationSubdivisionSource? value)
        => value switch
        {
            ObservationSubdivisionSource.Manual => "Вручну",
            ObservationSubdivisionSource.Layer => "Шар",
            ObservationSubdivisionSource.Rm => "Р/М",
            ObservationSubdivisionSource.Note => "Примітка",
            ObservationSubdivisionSource.Derived => "Похідне",
            ObservationSubdivisionSource.Import => "Імпорт",
            _ => "—"
        };

    /// <summary>
    /// Повертає текст типу мітки.
    /// </summary>
    private static string GetTagKindText(TagKind value)
        => value switch
        {
            TagKind.Keyword => "Ключове",
            TagKind.Person => "Людина",
            TagKind.Location => "Місце",
            TagKind.Subdivision => "Підрозділ",
            TagKind.Callsign => "Позивний",
            _ => "Інше"
        };

    /// <summary>
    /// Повертає текст джерела ймовірної дії.
    /// </summary>
    private static string GetProbableSourceText(ProbableActionSource value)
        => value switch
        {
            ProbableActionSource.Manual => "Вручну",
            ProbableActionSource.Rule => "Правило",
            ProbableActionSource.Derived => "Похідне",
            _ => value.ToString()
        };

    /// <summary>
    /// Форматує коефіцієнт впевненості у відсотки.
    /// </summary>
    private static string GetPercent(decimal value)
        => $"{Math.Round(value * 100m, 0):0}%";

    #endregion

    #region Disposal

    /// <summary>
    /// Вивільняє ресурси сторінки.
    /// </summary>
    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();

        GC.SuppressFinalize(this);
    }

    #endregion
}
