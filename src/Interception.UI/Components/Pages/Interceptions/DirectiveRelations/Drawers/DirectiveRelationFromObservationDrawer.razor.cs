//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Analytics.Dtos;
using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Interceptions.DirectiveRelations.Drawers;

public partial class DirectiveRelationFromObservationDrawer : ComponentBase
{
    [Inject] private IPersonDirectiveRelationService PersonDirectiveRelationService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public Guid? ObservationId { get; set; }
    [Parameter] public DateTime? ObservedDate { get; set; }
    [Parameter] public IReadOnlyList<ParticipantBriefDto>? Participants { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }

    private bool _loading;
    private bool _saving;
    private bool _initialized;
    private Guid? _loadedObservationId;
    private string? _warning;
    private IReadOnlyList<PersonDirectiveRelationOptionDto> _options = [];
    private IReadOnlyList<string> _participantLabels = [];
    private PersonDirectiveRelationSaveDto _form = CreateDefaultForm();
    private string? _fromSelection;
    private string? _toSelection;

    private bool CanSave =>
        (_form.FromCanonicalPersonId.HasValue || _form.FromResolvedParticipantId.HasValue)
        && (_form.ToCanonicalPersonId.HasValue || _form.ToResolvedParticipantId.HasValue);

    protected override async Task OnParametersSetAsync()
    {
        if (!IsOpen)
        {
            _initialized = false;
            _loadedObservationId = null;
            return;
        }

        if (_initialized && _loadedObservationId == ObservationId)
            return;

        _initialized = true;
        _loadedObservationId = ObservationId;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _warning = null;
        _form = CreateDefaultForm();
        _form.SourceObservationId = ObservationId;
        _fromSelection = null;
        _toSelection = null;
        _participantLabels = BuildParticipantLabels();
        await InvokeAsync(StateHasChanged);

        try
        {
            var participantNames = BuildParticipantNames();
            if (participantNames.Count < 2)
            {
                _options = [];
                _warning = "Для фіксації факту командування потрібні щонайменше дві відомі особи у спостереженні.";
                return;
            }

            _options = await PersonDirectiveRelationService.GetIdentityOptionsAsync(participantNames);
            if (_options.Count < 2)
                _warning = "Не вдалося однозначно зіставити учасників спостереження з підтвердженими/об’єднаними особами. Спершу перевірте особи в реєстрах або на сторінці об’єднання.";
        }
        catch (Exception ex)
        {
            _options = [];
            _warning = ex.Message;
        }
        finally
        {
            _loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private IReadOnlyList<string> BuildParticipantNames()
        => [.. (Participants ?? [])
            .Select(x => x.IsUnknown ? x.ResolvedName : x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private IReadOnlyList<string> BuildParticipantLabels()
        => [.. (Participants ?? [])
            .Select(x => x.DisplayName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private void OnDrawerClosed()
    {
        _initialized = false;
        _loadedObservationId = null;
        _warning = null;
        _options = [];
        _participantLabels = [];
        _form = CreateDefaultForm();
        _fromSelection = null;
        _toSelection = null;
    }

    private async Task SaveAsync()
    {
        if (ObservationId is null)
            return;

        var hasFrom = _form.FromCanonicalPersonId.HasValue || _form.FromResolvedParticipantId.HasValue;
        var hasTo = _form.ToCanonicalPersonId.HasValue || _form.ToResolvedParticipantId.HasValue;
        if (!hasFrom || !hasTo)
        {
            Toasts.Warning("Неповні дані", "Оберіть обидві особи для фіксації факту командування.");
            return;
        }

        _saving = true;
        try
        {
            _form.SourceObservationId = ObservationId;
            _form.IsManual = true;
            await PersonDirectiveRelationService.SaveAsync(_form);
            Toasts.Success("Факт збережено", "Контур керування оновлено для цього спостереження.");
            await CloseAsync();
            await OnSaved.InvokeAsync();
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

    private async Task CloseAsync()
        => await IsOpenChanged.InvokeAsync(false);

    private void OnFromSelectionChanged(ChangeEventArgs args)
    {
        _fromSelection = args.Value?.ToString();
        ApplySelection(_fromSelection, true);
    }

    private void OnToSelectionChanged(ChangeEventArgs args)
    {
        _toSelection = args.Value?.ToString();
        ApplySelection(_toSelection, false);
    }

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

    private static string BuildOptionValue(PersonDirectiveRelationOptionDto option)
        => $"{(option.IsCanonicalPerson ? "canonical" : "resolved")}:{option.IdentityId}";

    private static string BuildOptionLabel(PersonDirectiveRelationOptionDto option)
        => $"{option.DisplayName} [{option.KindLabel}]";

    private static string BuildRelationTypeLabel(DirectiveRelationType relationType)
        => relationType switch
        {
            DirectiveRelationType.Command => "Наказ / завдання",
            DirectiveRelationType.ReportUp => "Доповідь вгору",
            DirectiveRelationType.Control => "Контроль",
            DirectiveRelationType.Correction => "Коригування",
            DirectiveRelationType.Coordination => "Координація",
            DirectiveRelationType.Other => "Інше",
            _ => relationType.ToString()
        };

    private static string BuildConfidenceLabel(DirectiveRelationConfidence confidence)
        => confidence switch
        {
            DirectiveRelationConfidence.Low => "Низька",
            DirectiveRelationConfidence.Medium => "Середня",
            DirectiveRelationConfidence.High => "Висока",
            _ => confidence.ToString()
        };

    private static PersonDirectiveRelationSaveDto CreateDefaultForm()
        => new()
        {
            RelationType = DirectiveRelationType.Command,
            Confidence = DirectiveRelationConfidence.High,
            IsManual = true
        };
}
