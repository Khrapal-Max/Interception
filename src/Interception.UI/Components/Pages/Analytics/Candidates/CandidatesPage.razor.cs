//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.Candidates;

public partial class CandidatesPage : ComponentBase
{
    [Inject] private IParticipantCandidateGroupQueryService ParticipantCandidateGroupQueryService { get; set; } = default!;
    [Inject] private IParticipantCandidateAnalysisService ParticipantCandidateAnalysisService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private PagedResultDto<CandidateGroupDto>? _pagedResult;
    private bool _loading;
    private bool _running;
    private int _currentPage = 1;
    private const int PageSize = 50;

    // Лічильник Open — для бейджа на табі, завжди актуальний
    private int _openCount;

    private CandidateGroupStatusDto _activeTab = CandidateGroupStatusDto.Open;

    private bool _drawerOpen;
    private CandidateGroupDto? _selectedGroup;

    protected override async Task OnInitializedAsync()
    {
        await LoadOpenCountAsync();
        await LoadPageAsync();
    }

    // -------------------------------------------------------------------------
    // Дані
    // -------------------------------------------------------------------------

    internal async Task LoadPageAsync()
    {
        _loading = true;
        StateHasChanged();
        try
        {
            _pagedResult = await ParticipantCandidateGroupQueryService.GetGroupsByStatusAsync(
                _activeTab, _currentPage, PageSize);
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка завантаження", ex.Message);
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task LoadOpenCountAsync()
    {
        try
        {
            var open = await ParticipantCandidateGroupQueryService.GetGroupsByStatusAsync(
                CandidateGroupStatusDto.Open, 1, 1);
            _openCount = open.TotalCount;
        }
        catch { /* не критично */ }
    }

    private async Task GoToPageAsync(int page)
    {
        if (_pagedResult is null)
            return;

        _currentPage = Math.Clamp(page, 1, Math.Max(1, _pagedResult.TotalPages));
        await LoadPageAsync();
    }

    private IEnumerable<int?> GetVisiblePages()
    {
        if (_pagedResult is null || _pagedResult.TotalPages <= 1)
            yield break;

        var total = _pagedResult.TotalPages;
        var current = _currentPage;

        if (total <= 7)
        {
            for (var i = 1; i <= total; i++)
                yield return i;

            yield break;
        }

        yield return 1;

        var start = Math.Max(2, current - 1);
        var end = Math.Min(total - 1, current + 1);

        if (start > 2)
            yield return null;

        for (var i = start; i <= end; i++)
            yield return i;

        if (end < total - 1)
            yield return null;

        yield return total;
    }

    private async Task SwitchTabAsync(CandidateGroupStatusDto status)
    {
        _activeTab = status;
        _currentPage = 1;
        await LoadPageAsync();
    }

    // -------------------------------------------------------------------------
    // Аналіз
    // -------------------------------------------------------------------------

    private async Task RunAnalysisAsync()
    {
        _running = true;
        try
        {
            var created = await ParticipantCandidateAnalysisService.RunAsync();
            Toasts.Success("Аналіз завершено",
                created > 0
                    ? $"Знайдено {created} нових груп кандидатів."
                    : "Нових груп не знайдено.");

            await LoadOpenCountAsync();
            await LoadPageAsync();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка аналізу", ex.Message);
        }
        finally
        {
            _running = false;
        }
    }

    // -------------------------------------------------------------------------
    // Дравер
    // -------------------------------------------------------------------------

    private void OpenGroup(CandidateGroupDto group)
    {
        _selectedGroup = group;
        _drawerOpen = true;
    }

    private async Task OnGroupClosed()
    {
        await LoadOpenCountAsync();
        await LoadPageAsync();
    }

    // -------------------------------------------------------------------------
    // UI helpers
    // -------------------------------------------------------------------------

    private static string ConfidenceBarClass(double score) => score switch
    {
        >= 0.75 => "bg-success",
        >= 0.50 => "bg-warning",
        _ => "bg-danger"
    };
}
