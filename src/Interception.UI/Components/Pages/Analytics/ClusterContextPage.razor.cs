//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos.AnalyticsClusters;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics;

public partial class ClusterContextPage : ComponentBase
{
    [Inject] public IAnalyticsClusterContextService ContextService { get; set; } = default!;
    [Inject] public ToastService ToastService { get; set; } = default!;

    [Parameter] public Guid ClusterId { get; set; }

    protected AnalyticsClusterContextPageDto? _dto;
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
            _dto = await ContextService.GetPageAsync(ClusterId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ToastService.Error("Помилка завантаження контексту гіпотези.", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    protected static string GetRelationBadgeClass(string relationKind)
        => relationKind switch
        {
            "strong-indirect" => "text-bg-warning",
            "medium-indirect" => "text-bg-secondary",
            "direct" => "text-bg-success",
            _ => "text-bg-light"
        };
}