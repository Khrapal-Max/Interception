//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Registry.Dtos;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Registry.CommandContour;

/// <summary>
/// Проста сторінка ручного ведення зв'язків структурного керування.
/// </summary>
public partial class CommandContourPage : ComponentBase
{
    [Inject] private ICommandContourService CommandContourService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    protected IReadOnlyList<CommandContourListItemDto> _rows = [];
    protected IReadOnlyList<CommandContourOptionDto> _options = [];
    protected CommandContourSaveDto _form = CreateDefaultForm();
    protected bool _loading;
    protected bool _saving;
    protected string? _fromSelection;
    protected string? _toSelection;
    protected CommandContourListItemDto? _detailsRow;
    protected bool _detailsOpen;
    protected bool _deleteConfirmOpen;
    protected Guid? _pendingDeleteId;
    protected bool _deleteInProgress;
    protected string? _deleteReason;

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    internal async Task LoadAsync()
    {
        _loading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            _rows = await CommandContourService.GetAllAsync();
            _options = await CommandContourService.GetIdentityOptionsAsync(null);
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка завантаження", ex.Message);
            _rows = [];
            _options = [];
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected async Task SaveAsync()
    {
        if (!HasFromSelection() || !HasToSelection())
        {
            Toasts.Warning("Неповні дані", "Оберіть обидві особи.");
            return;
        }

        _saving = true;
        try
        {
            await CommandContourService.SaveAsync(_form);
            Toasts.Success("Зв'язок збережено", "Контур керування оновлено.");

            _form = CreateDefaultForm();
            _fromSelection = null;
            _toSelection = null;

            await LoadAsync();
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

    protected void OpenDetails(CommandContourListItemDto row)
    {
        _detailsRow = row;
        _detailsOpen = true;
    }

    protected void CloseDetails()
    {
        _detailsOpen = false;
        _detailsRow = null;
    }

    protected void AskDelete(Guid id)
    {
        _pendingDeleteId = id;
        _deleteReason = null;
        _deleteConfirmOpen = true;
    }

    protected void CancelDelete()
    {
        if (_deleteInProgress)
            return;

        _deleteConfirmOpen = false;
        _pendingDeleteId = null;
        _deleteReason = null;
    }

    protected async Task ConfirmDeleteAsync()
    {
        if (_pendingDeleteId is null)
            return;

        if (string.IsNullOrWhiteSpace(_deleteReason))
        {
            Toasts.Warning("Потрібна причина", "Вкажіть коротку причину видалення.");
            return;
        }

        _deleteInProgress = true;
        try
        {
            await CommandContourService.DeleteAsync(_pendingDeleteId.Value);
            Toasts.Success("Зв'язок видалено", $"Запис прибрано. Причина: {_deleteReason.Trim()}.");
            _deleteConfirmOpen = false;
            _pendingDeleteId = null;
            _deleteReason = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка видалення", ex.Message);
        }
        finally
        {
            _deleteInProgress = false;
        }
    }

    protected void OnFromSelectionChanged(ChangeEventArgs args)
    {
        _fromSelection = args.Value?.ToString();
        ApplySelection(_fromSelection, isFrom: true);
    }

    protected void OnToSelectionChanged(ChangeEventArgs args)
    {
        _toSelection = args.Value?.ToString();
        ApplySelection(_toSelection, isFrom: false);
    }

    protected static string BuildRelationTypeLabel(CommandContourRelationTypeDto relationType)
        => relationType switch
        {
            CommandContourRelationTypeDto.Command => "Наказ / завдання",
            CommandContourRelationTypeDto.ReportUp => "Доповідь вгору",
            CommandContourRelationTypeDto.Control => "Контроль",
            CommandContourRelationTypeDto.Correction => "Коригування",
            CommandContourRelationTypeDto.Coordination => "Координація",
            CommandContourRelationTypeDto.Other => "Інше",
            _ => relationType.ToString()
        };

    protected static string BuildConfidenceLabel(CommandContourConfidenceDto confidence)
        => confidence switch
        {
            CommandContourConfidenceDto.Low => "Низька",
            CommandContourConfidenceDto.Medium => "Середня",
            CommandContourConfidenceDto.High => "Висока",
            _ => confidence.ToString()
        };

    protected static string BuildOptionValue(CommandContourOptionDto option)
        => $"{(option.IsCanonicalPerson ? "canonical" : "resolved")}:{option.IdentityId}";

    protected static string BuildOptionLabel(CommandContourOptionDto option)
        => $"{option.DisplayName} [{option.KindLabel}]";

    private bool HasFromSelection()
        => _form.FromCanonicalPersonId.HasValue || _form.FromResolvedParticipantId.HasValue;

    private bool HasToSelection()
        => _form.ToCanonicalPersonId.HasValue || _form.ToResolvedParticipantId.HasValue;

    private void ApplySelection(string? value, bool isFrom)
    {
        if (isFrom)
        {
            _form.FromCanonicalPersonId = null;
            _form.FromResolvedParticipantId = null;
        }
        else
        {
            _form.ToCanonicalPersonId = null;
            _form.ToResolvedParticipantId = null;
        }

        if (string.IsNullOrWhiteSpace(value))
            return;

        var parts = value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id))
            return;

        var isCanonical = string.Equals(parts[0], "canonical", StringComparison.OrdinalIgnoreCase);

        if (isFrom)
        {
            if (isCanonical)
                _form.FromCanonicalPersonId = id;
            else
                _form.FromResolvedParticipantId = id;
        }
        else
        {
            if (isCanonical)
                _form.ToCanonicalPersonId = id;
            else
                _form.ToResolvedParticipantId = id;
        }
    }

    private static CommandContourSaveDto CreateDefaultForm()
        => new()
        {
            RelationType = CommandContourRelationTypeDto.Command,
            Confidence = CommandContourConfidenceDto.High,
            IsManual = true
        };
}
