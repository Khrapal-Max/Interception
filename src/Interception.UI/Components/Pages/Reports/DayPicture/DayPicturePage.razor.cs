//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Reports.Abstractions;
using Interception.UI.Application.Reports.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain.Enums;
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

    private DayPictureDto? _picture;
    private bool _loading;
    private DateTime? _day = DateTime.Today;
    private string? _locationClassFilter;

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

    private static List<DayPictureEntryDto> GetOrderedEntries(DayPictureGroupDto group)
        => group.Conversations
            .SelectMany(x => x.Entries)
            .OrderBy(x => x.ObservedDate)
            .ToList();

    private static IReadOnlyList<DayPictureEntryDto> ApplyLocationClassFilter(
        IReadOnlyList<DayPictureEntryDto> entries,
        string? locationClassFilter)
    {
        if (string.IsNullOrWhiteSpace(locationClassFilter))
            return entries;

        return [.. entries.Where(x => string.Equals(x.LocationClass, locationClassFilter, StringComparison.OrdinalIgnoreCase))];
    }

    private IEnumerable<(string Key, int Count)> BuildActionLocationGroups(IReadOnlyList<DayPictureEntryDto> entries)
        => entries
            .GroupBy(
                x => $"{(string.IsNullOrWhiteSpace(x.ActionName) ? "—" : x.ActionName)} × {x.LocationClass}",
                StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(x => x.Item2)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> GetLocationClassOptions()
        => [.. Enum.GetNames<LocationClass>()];
}
