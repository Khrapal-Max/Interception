//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Interceptions.DirectiveRelations;

/// <summary>
/// Проста сторінка ручного ведення зв'язків структурного керування.
/// </summary>
public partial class DirectiveRelationsPage : ComponentBase
{
    [Inject] private IPersonDirectiveRelationService PersonDirectiveRelationService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    protected IReadOnlyList<PersonDirectiveRelationListItemDto> _rows = [];
    protected IReadOnlyList<PersonDirectiveRelationOptionDto> _options = [];
    protected PersonDirectiveRelationSaveDto _form = CreateDefaultForm();
    protected bool _loading;
    protected bool _saving;
    protected string? _fromSelection;
    protected string? _toSelection;

    protected override async Task OnInitializedAsync()
        => await LoadAsync();

    internal async Task LoadAsync()
    {
        _loading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            _rows = await PersonDirectiveRelationService.GetAllAsync();
            _options = await PersonDirectiveRelationService.GetIdentityOptionsAsync(null);
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
            await PersonDirectiveRelationService.SaveAsync(_form);
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

    protected async Task DeleteAsync(Guid id)
    {
        try
        {
            await PersonDirectiveRelationService.DeleteAsync(id);
            Toasts.Success("Зв'язок видалено", "Запис контуру керування прибрано.");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Toasts.Error("Помилка видалення", ex.Message);
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

    protected static string BuildRelationTypeLabel(DirectiveRelationTypeDto relationType)
        => relationType switch
        {
            DirectiveRelationTypeDto.Command => "Наказ / завдання",
            DirectiveRelationTypeDto.ReportUp => "Доповідь вгору",
            DirectiveRelationTypeDto.Control => "Контроль",
            DirectiveRelationTypeDto.Correction => "Коригування",
            DirectiveRelationTypeDto.Coordination => "Координація",
            DirectiveRelationTypeDto.Other => "Інше",
            _ => relationType.ToString()
        };

    protected static string BuildConfidenceLabel(DirectiveRelationConfidenceDto confidence)
        => confidence switch
        {
            DirectiveRelationConfidenceDto.Low => "Низька",
            DirectiveRelationConfidenceDto.Medium => "Середня",
            DirectiveRelationConfidenceDto.High => "Висока",
            _ => confidence.ToString()
        };

    protected static string BuildOptionValue(PersonDirectiveRelationOptionDto option)
        => $"{(option.IsCanonicalPerson ? "canonical" : "resolved")}:{option.IdentityId}";

    protected static string BuildOptionLabel(PersonDirectiveRelationOptionDto option)
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

    private static PersonDirectiveRelationSaveDto CreateDefaultForm()
        => new()
        {
            RelationType = DirectiveRelationTypeDto.Command,
            Confidence = DirectiveRelationConfidenceDto.High,
            IsManual = true
        };
}
