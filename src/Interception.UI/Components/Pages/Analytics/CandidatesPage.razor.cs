//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Candidates;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics;

public partial class CandidatesPage : ComponentBase
{
    [Inject] private IParticipantCandidateGroupQueryService ParticipantCandidateGroupQueryService { get; set; } = default!;
    [Inject] private IParticipantCandidateAnalysisService ParticipantCandidateAnalysisService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private PagedResult<CandidateGroupDto>? _pagedResult;
    private bool _loading;
    private bool _running;
    private int _currentPage = 1;
    private const int PageSize = 50;

    // Лічильник Open — для бейджа на табі, завжди актуальний
    private int _openCount;

    private CandidateGroupStatus _activeTab = CandidateGroupStatus.Open;

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
                CandidateGroupStatus.Open, 1, 1);
            _openCount = open.TotalCount;
        }
        catch { /* не критично */ }
    }

    private async Task GoToPageAsync(int page)
    {
        _currentPage = page;
        await LoadPageAsync();
    }

    private async Task SwitchTabAsync(CandidateGroupStatus status)
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
