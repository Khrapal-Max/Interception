//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos.AnalyticsUnknownClusters;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics;

/// <summary>
/// Сторінка реєстру аналітичних кластерів невизначених осіб.
/// </summary>
public partial class UnknownClustersRegistryPage : ComponentBase
{
    [Inject] public IAnalyticsUnknownClusterRegistryService RegistryService { get; set; } = default!;
    [Inject] public ToastService ToastService { get; set; } = default!;

    protected AnalyticsUnknownClusterRegistryPageDto? _page;
    protected bool _loading;
    protected string? _query;
    protected string? _status;
    protected int _pageIndex;
    protected int _take = 20;

    protected int TotalPages => _page is null ? 1 : Math.Max(1, (int)Math.Ceiling(_page.Total / (double)_take));
    protected bool CanPrev => _pageIndex > 0;
    protected bool CanNext => _pageIndex + 1 < TotalPages;

    protected override async Task OnInitializedAsync()
    {
        await SearchAsync();
    }

    protected async Task SearchAsync()
    {
        _loading = true;

        try
        {
            _page = await RegistryService.SearchAsync(BuildFilter(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            ToastService.Error("Не вдалося завантажити реєстр кластерів.", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    protected async Task ResetAsync()
    {
        _query = null;
        _status = null;
        _pageIndex = 0;
        await SearchAsync();
    }

    protected async Task PrevPageAsync()
    {
        if (!CanPrev)
            return;

        _pageIndex--;
        await SearchAsync();
    }

    protected async Task NextPageAsync()
    {
        if (!CanNext)
            return;

        _pageIndex++;
        await SearchAsync();
    }

    protected static string GetStatusText(string? status)
        => (status ?? string.Empty).ToLowerInvariant() switch
        {
            "open" => "Відкрита",
            "archived" => "Архів",
            _ => "Невідомо"
        };

    protected static string GetStatusBadgeClass(string? status)
        => (status ?? string.Empty).ToLowerInvariant() switch
        {
            "open" => "text-bg-warning",
            "archived" => "text-bg-dark",
            _ => "text-bg-light"
        };

    protected static string GetArchiveReasonText(string? reason)
        => (reason ?? string.Empty).ToLowerInvariant() switch
        {
            "resolved" => "Встановлено особу",
            "merged" => "Об'єднано",
            "empty" => "Порожня після переносу",
            "archived" => "Архівовано",
            _ => "—"
        };

    private AnalyticsUnknownClusterRegistryFilter BuildFilter()
        => new()
        {
            Query = string.IsNullOrWhiteSpace(_query) ? null : _query.Trim(),
            Status = string.IsNullOrWhiteSpace(_status) ? null : _status.Trim(),
            Skip = _pageIndex * _take,
            Take = _take
        };
}