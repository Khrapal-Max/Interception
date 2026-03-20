//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Observations;

public partial class ObservationDetailsPage : ComponentBase, IDisposable
{
    [Parameter] public Guid ObservationId { get; set; }

    private readonly CancellationTokenSource _lifetimeCts = new();

    private ObservationDetailsDto? _details;
    private bool _loading;
    private bool _editDrawerOpen;

    [Inject] private IObservationRegistryService RegistryService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    private async Task ReloadAsync()
    {
        await LoadAsync();
    }

    private void GoBack() => Navigation.NavigateTo("/observations");

    private void OpenEditDrawer()
    {
        if (_details is not null)
            _editDrawerOpen = true;
    }

    private Task HandleEditDrawerChangedAsync(bool isOpen)
    {
        _editDrawerOpen = isOpen;
        return Task.CompletedTask;
    }

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

    public void Dispose()
    {
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();

        GC.SuppressFinalize(this);
    }

    private static string GetSubdivisionStrengthText(SubdivisionLinkStrength? value)
        => value switch
        {
            SubdivisionLinkStrength.Weak => "Слабкий",
            SubdivisionLinkStrength.Medium => "Середній",
            SubdivisionLinkStrength.Strong => "Сильний",
            _ => "—"
        };

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

    private static string GetProbableSourceText(ProbableActionSource value)
        => value switch
        {
            ProbableActionSource.Manual => "Вручну",
            ProbableActionSource.Rule => "Правило",
            ProbableActionSource.Derived => "Похідне",
            _ => value.ToString()
        };

    private static string GetPercent(decimal value)
        => $"{Math.Round(value * 100m, 0):0}%";
}
