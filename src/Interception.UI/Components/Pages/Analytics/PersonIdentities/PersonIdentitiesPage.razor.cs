//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.PersonIdentities;

/// <summary>
/// Аналітична сторінка кандидатів на злиття в канонічну особу.
/// </summary>
public partial class PersonIdentitiesPage : ComponentBase
{
    [Inject] private ICanonicalPersonAnalysisService CanonicalPersonAnalysisService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    protected IReadOnlyList<CanonicalPersonCandidateDto>? _candidates;
    protected CanonicalPersonCandidateDetailsDto? _selected;
    protected bool _loading;
    protected bool _saving;
    protected string? _note;
    protected HashSet<Guid> _selectedRows = [];

    protected int TotalCandidates => _candidates?.Count ?? 0;
    protected int ReadyCanonicalCount => _candidates?.Count(x => x.HasCanonicalPerson) ?? 0;

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    internal async Task LoadAsync()
    {
        _loading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            _candidates = await CanonicalPersonAnalysisService.GetCandidatesAsync();
            var selectedKey = _selected?.CandidateKey;

            if (!string.IsNullOrWhiteSpace(selectedKey))
                await LoadDetailsAsync(selectedKey);
            else
                await LoadDetailsAsync(_candidates.FirstOrDefault()?.CandidateKey);
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка завантаження", ex.Message);
            _candidates = [];
            _selected = null;
            _selectedRows = [];
            _note = null;
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected async Task SelectCandidateAsync(CanonicalPersonCandidateDto candidate)
        => await LoadDetailsAsync(candidate.CandidateKey);

    protected bool IsChecked(Guid id) => _selectedRows.Contains(id);

    protected void ToggleRow(Guid id, object? value)
    {
        var isChecked = value as bool? == true;
        if (isChecked)
            _selectedRows.Add(id);
        else
            _selectedRows.Remove(id);
    }

    protected async Task SaveAsync()
    {
        if (_selected is null)
            return;

        if (_selectedRows.Count == 0)
        {
            Toasts.Warning("Немає вибору", "Оберіть хоча б один підтверджений рядок.");
            return;
        }

        _saving = true;
        try
        {
            _selected = _selected.HasCanonicalPerson && _selected.CanonicalPersonId.HasValue
                ? await CanonicalPersonAnalysisService.AttachToCanonicalAsync(_selected.CanonicalPersonId.Value, [.. _selectedRows], _note)
                : await CanonicalPersonAnalysisService.CreateCanonicalAsync(_selected.CandidateKey, [.. _selectedRows], _note);

            _note = _selected.CanonicalNote;
            _selectedRows = [.. _selected.Rows.Where(x => !x.IsLinkedToCanonical).Select(x => x.ResolvedParticipantId)];

            Toasts.Success(
                _selected.HasCanonicalPerson ? "Канонічну особу оновлено" : "Канонічну особу створено",
                $"Кандидат '{_selected.DisplayName}' оброблено.");

            _candidates = await CanonicalPersonAnalysisService.GetCandidatesAsync();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка збереження", ex.Message);
        }
        finally
        {
            _saving = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadDetailsAsync(string? candidateKey)
    {
        if (string.IsNullOrWhiteSpace(candidateKey))
        {
            _selected = null;
            _selectedRows = [];
            _note = null;
            return;
        }

        _selected = await CanonicalPersonAnalysisService.GetCandidateDetailsAsync(candidateKey);
        _note = _selected?.CanonicalNote;
        _selectedRows = _selected is null
            ? []
            : [.. _selected.Rows.Where(x => !x.IsLinkedToCanonical).Select(x => x.ResolvedParticipantId)];
    }
}
