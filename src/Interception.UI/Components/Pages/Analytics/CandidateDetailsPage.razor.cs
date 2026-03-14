//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics;

public partial class CandidateDetailsPage : ComponentBase
{
    [Inject] public IAnalyticsCandidateService CandidateService { get; set; } = default!;
    [Inject] public ToastService ToastService { get; set; } = default!;

    [Parameter] public string CandidateKey { get; set; } = string.Empty;

    protected AnalyticsCandidateDetailsDto? _dto;
    protected bool _loading;

    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;

        try
        {
            _dto = await CandidateService.GetDetailsAsync(CandidateKey, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ToastService.Error("Помилка завантаження деталізації кандидата.", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    protected static string GetCandidateTypeText(string type)
        => type switch
        {
            "open-hypothesis" => "Відкрита гіпотеза",
            "unresolved-unknown" => "Невизначений raw",
            _ => type
        };

    protected static string GetPriorityBandText(string band)
        => band switch
        {
            "high" => "Високий",
            "medium" => "Середній",
            _ => "Низький"
        };

    protected static string GetPriorityBadgeClass(string band)
        => band switch
        {
            "high" => "text-bg-danger",
            "medium" => "text-bg-warning",
            _ => "text-bg-secondary"
        };
}