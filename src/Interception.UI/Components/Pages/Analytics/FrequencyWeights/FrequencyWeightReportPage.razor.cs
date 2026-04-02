//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.FrequencyWeights;

/// <summary>
/// Проста аналітична сторінка ваг підрозділів по частотах.
/// </summary>
public partial class FrequencyWeightReportPage : ComponentBase
{
    [Inject] private IFrequencyWeightReportService FrequencyWeightReportService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private FrequencyWeightReportDto? _report;
    private bool _loading;

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    internal async Task LoadAsync()
    {
        _loading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            _report = await FrequencyWeightReportService.BuildAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка завантаження", ex.Message);
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}
