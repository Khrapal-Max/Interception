//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Reports.Abstractions;
using Interception.UI.Application.Reports.Models;
using Interception.UI.Application.Toasts;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Reports.DayPicture;

/// <summary>
/// Сторінка картини дня.
/// </summary>
public partial class DayPicturePage : ComponentBase
{
    [Inject] private IDayPictureService DayPictureService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    private DayPictureModel? _picture;
    private bool _loading;
    private DateTime? _day = DateTime.Today;

    private string? DayStr => ConverterDateTimeExtensions.FormatDate(_day);

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    internal async Task LoadAsync()
    {
        _loading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var day = DateOnly.FromDateTime((_day ?? DateTime.Today).Date);
            _picture = await DayPictureService.BuildAsync(day, CancellationToken.None);
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

    private void OnDayChanged(string? value)
        => _day = ConverterDateTimeExtensions.ParseDate(value);

    private async Task OnDayInputChanged(ChangeEventArgs args)
    {
        OnDayChanged(args.Value?.ToString());
        await LoadAsync();
    }

    private async Task ResetDayAsync()
    {
        _day = DateTime.Today;
        await LoadAsync();
    }

    private static List<DayPictureEntryModel> GetOrderedEntries(DayPictureGroupModel group)
        => group.Conversations
            .SelectMany(x => x.Entries)
            .OrderBy(x => x.ObservedDate)
            .ToList();
}
