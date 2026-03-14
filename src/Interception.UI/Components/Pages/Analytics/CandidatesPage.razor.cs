//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos.AnalyticsCandidates;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics;

public partial class CandidatesPage : ComponentBase
{
    [Inject] public IAnalyticsCandidateService CandidateService { get; set; } = default!;
    [Inject] public ToastService ToastService { get; set; } = default!;

    protected AnalyticsCandidatePageDto? _page;
    protected bool _loading;

    protected string? _query;
    protected string? _candidateType;
    protected bool _onlyWithoutHypothesis;

    protected int _pageIndex;
    protected int _take = 8;

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
            _page = await CandidateService.GetPageAsync(
                new AnalyticsCandidateFilterDto(
                    _query,
                    _candidateType,
                    _onlyWithoutHypothesis,
                    null,
                    null,
                    _pageIndex * _take,
                    _take),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            ToastService.Error("Помилка завантаження черги аналізу.", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    protected async Task ResetAsync()
    {
        _query = null;
        _candidateType = null;
        _onlyWithoutHypothesis = false;
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

    protected static string GetCandidateTypeText(string type)
        => type switch
        {
            "open-hypothesis" => "Відкрите припущення",
            "unresolved-unknown" => "Невідома особа",
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