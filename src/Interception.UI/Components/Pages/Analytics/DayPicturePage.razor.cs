//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos.DayPicture;
using Interception.UI.Application.Analytics.Enums;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics;

public partial class DayPicturePage : ComponentBase
{
    [Inject] public IAnalyticsDayPictureService DayPictureService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [Inject] public ToastService ToastService { get; set; } = default!;

    [Parameter] public string? DateText { get; set; }

    protected AnalyticsDayPicturePageDto? _dto;
    protected bool _loading;
    protected DateTime _dateInput = DateTime.Today;

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrWhiteSpace(DateText) &&
            DateOnly.TryParse(DateText, out var parsed))
        {
            _dateInput = parsed.ToDateTime(TimeOnly.MinValue);
        }

        await LoadAsync();
    }

    protected async Task LoadAsync()
    {
        _loading = true;

        try
        {
            var date = DateOnly.FromDateTime(_dateInput);
            _dto = await DayPictureService.GetPageAsync(date, CancellationToken.None);
            Navigation.NavigateTo($"/analytics/day/{date:yyyy-MM-dd}", replace: true);
        }
        catch (Exception ex)
        {
            ToastService.Error("Помилка завантаження картини дня.", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    protected static string GetReadinessText(AnalyticsCandidateReadiness readiness)
        => readiness switch
        {
            AnalyticsCandidateReadiness.EnoughForHypothesis => "Достатньо для припущення",
            AnalyticsCandidateReadiness.LikelyExistingHypothesis => "Ймовірно існуюче припущення",
            AnalyticsCandidateReadiness.LikelyFactPattern => "Ймовірний факт-патерн",
            _ => "Ще недостатньо"
        };
}