//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Abstractions;
using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Analytics.PersonIdentities;

/// <summary>
/// Аналітична сторінка кандидатів на об’єднання в один профіль.
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
    protected string? _displayName;
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
                await LoadDetailsAsync(_candidates?.FirstOrDefault()?.CandidateKey);
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка завантаження", ex.Message);
            _candidates = [];
            _selected = null;
            _selectedRows = [];
            _note = null;
            _displayName = null;
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

    protected void ToggleRow(Guid id, bool isChecked)
    {
        if (isChecked)
            _selectedRows.Add(id);
        else
            _selectedRows.Remove(id);
    }

    protected async Task SaveAsync()
    {
        if (_selected is null)
            return;

        if (string.IsNullOrWhiteSpace(_displayName))
        {
            Toasts.Warning("Немає назви профілю", "Вкажіть назву об’єднаного профілю.");
            return;
        }

        _saving = true;
        try
        {
            var shouldCreateSeparateProfile = ShouldCreateSeparateProfile();

            var createdNew = shouldCreateSeparateProfile;

            _selected = shouldCreateSeparateProfile
                ? await CanonicalPersonAnalysisService.CreateCanonicalAsync(_selected.CandidateKey, [.. _selectedRows], _displayName, _note)
                : await CanonicalPersonAnalysisService.AttachToCanonicalAsync(_selected.CanonicalPersonId!.Value, [.. _selectedRows], _note);

            _displayName = _selected.CanonicalDisplayName ?? _selected.DisplayName;
            _note = _selected.CanonicalNote;
            await ResetSelectionFromDetailsAsync();

            Toasts.Success(
                createdNew ? "Об’єднаний профіль створено" : "Об’єднаний профіль оновлено",
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

    protected string GetProfileLabel(CanonicalPersonCandidateRowDto row)
    {
        if (_selected is null)
            return "—";

        var profileIndexMap = BuildProfileIndexMap();

        if (row.IsLinkedToCanonical && row.CanonicalPersonId.HasValue && profileIndexMap.TryGetValue(row.CanonicalPersonId.Value, out var linkedIndex))
            return $"Профіль {linkedIndex}";

        if (_selected.HasSingleProfile && _selectedRows.Contains(row.ResolvedParticipantId) && ShouldCreateSeparateProfile())
            return $"Профіль {profileIndexMap.Count + 1} (новий)";

        return "Без профілю";
    }

    protected string GetProfileBadgeClass(CanonicalPersonCandidateRowDto row)
    {
        if (row.IsLinkedToCanonical)
            return "text-bg-success";

        if (_selected?.HasSingleProfile == true && _selectedRows.Contains(row.ResolvedParticipantId) && ShouldCreateSeparateProfile())
            return "text-bg-warning";

        return "text-bg-secondary";
    }

    protected string GetSaveActionLabel()
    {
        if (_selected is null)
            return "Зберегти";

        if (!ShouldCreateSeparateProfile())
            return "Додати в профіль";

        if (_selected.HasSingleProfile)
            return "Створити окремий профіль";

        return _selected.HasMultipleProfiles
            ? "Звести в один профіль"
            : "Об’єднати в один профіль";
    }

    protected async Task UpdateAsync()
    {
        if (_selected?.CanonicalPersonId is null || !_selected.HasSingleProfile)
            return;

        if (string.IsNullOrWhiteSpace(_displayName))
        {
            Toasts.Warning("Немає назви профілю", "Вкажіть назву об’єднаного профілю.");
            return;
        }

        _saving = true;
        try
        {
            _selected = await CanonicalPersonAnalysisService.UpdateCanonicalAsync(
                _selected.CandidateKey,
                _selected.CanonicalPersonId.Value,
                _displayName,
                _note);

            _displayName = _selected.CanonicalDisplayName ?? _selected.DisplayName;
            _note = _selected.CanonicalNote;
            await ResetSelectionFromDetailsAsync();

            Toasts.Success("Об’єднаний профіль оновлено", $"Кандидат '{_selected.DisplayName}' оновлено.");
            _candidates = await CanonicalPersonAnalysisService.GetCandidatesAsync();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка оновлення профілю", ex.Message);
        }
        finally
        {
            _saving = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected async Task DeleteAsync()
    {
        if (_selected?.CanonicalPersonId is null || !_selected.HasSingleProfile)
            return;

        _saving = true;
        try
        {
            var candidateKey = _selected.CandidateKey;
            await CanonicalPersonAnalysisService.DeleteCanonicalAsync(_selected.CanonicalPersonId.Value);

            Toasts.Success("Об’єднаний профіль видалено", $"Кандидат '{_selected.DisplayName}' повернуто до окремих записів.");

            _candidates = await CanonicalPersonAnalysisService.GetCandidatesAsync();
            await LoadDetailsAsync(candidateKey);
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка видалення профілю", ex.Message);
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
            _displayName = null;
            return;
        }

        await CanonicalPersonAnalysisService.PrepareCandidateContextsAsync(candidateKey);
        _selected = await CanonicalPersonAnalysisService.GetCandidateDetailsAsync(candidateKey);
        _displayName = _selected?.CanonicalDisplayName ?? _selected?.DisplayName;
        _note = _selected?.CanonicalNote;
        await ResetSelectionFromDetailsAsync();
    }

    private Task ResetSelectionFromDetailsAsync()
    {
        if (_selected is null)
        {
            _selectedRows = [];
            return Task.CompletedTask;
        }

        _selectedRows = _selected.HasSingleProfile
            ? [.. _selected.Rows.Where(x => !x.IsLinkedToCanonical).Select(x => x.ResolvedParticipantId)]
            : [.. _selected.Rows.Select(x => x.ResolvedParticipantId)];

        return Task.CompletedTask;
    }

    private bool ShouldCreateSeparateProfile()
    {
        if (_selected is null)
            return false;

        var selectedRows = _selected.Rows
            .Where(x => _selectedRows.Contains(x.ResolvedParticipantId))
            .ToList();

        return !_selected.HasSingleProfile
            || selectedRows.All(x => !x.IsLinkedToCanonical);
    }

    private Dictionary<Guid, int> BuildProfileIndexMap()
    {
        if (_selected is null)
            return [];

        var canonicalIds = _selected.Rows
            .Where(x => x.IsLinkedToCanonical && x.CanonicalPersonId.HasValue)
            .Select(x => x.CanonicalPersonId!.Value)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        return canonicalIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index + 1);
    }
}
