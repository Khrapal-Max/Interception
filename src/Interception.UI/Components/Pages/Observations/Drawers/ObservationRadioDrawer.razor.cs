using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Observations.Models;
using Interception.UI.Components.Pages.Observations.Parsing;
using Interception.UI.Components.Shared.Drawer;
using Interception.UI.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Observations.Drawers;

public partial class ObservationRadioDrawer : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private CancellationTokenSource? _lookupCts;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public EventCallback<Guid> Saved { get; set; }

    [Inject] private IObservationWriteService WriteService { get; set; } = default!;
    [Inject] private IObservationLookupService LookupService { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;

    private Drawer? _drawer;
    private ObservationRadioDrawerModel _model = ObservationRadioDrawerModel.CreateEmpty();
    private bool _initialized;
    private bool _saving;
    private List<ActionCatalogSuggestionDto> _actionSuggestions = [];
    private string? _actionSearch;

    protected override void OnParametersSet()
    {
        if (!IsOpen)
        {
            _initialized = false;
            return;
        }

        if (_initialized)
            return;

        _initialized = true;
        _model = ObservationRadioDrawerModel.CreateEmpty();
        _actionSuggestions.Clear();
        _actionSearch = null;
    }

    private void ParseText()
    {
        var result = ObservationRadioTextParser.Parse(_model.RawText);

        if (!result.IsSuccess)
        {
            ToastService.Warning("Не вдалося розібрати блок", result.ErrorMessage ?? "Перевірте формат тексту.");
            _model.IsParsed = false;
            _model.SourcePost = null;
            _model.SuggestedActionRaw = null;
            _model.Draft = ObservationRadioDrawerModel.CreateEmpty().Draft;
            _model.SuggestedTags.Clear();
            _model.Warnings.Clear();
            return;
        }

        _model.IsParsed = true;
        _model.SourcePost = result.SourcePost;
        _model.SuggestedActionRaw = result.SuggestedActionRaw;
        _model.Draft = result.Draft;
        _model.SuggestedTags = result.SuggestedTags;
        _model.Warnings = result.Warnings;
        _actionSearch = _model.Draft.ActionRaw;

        if (_model.Warnings.Count > 0)
        {
            ToastService.Warning("Блок розібрано частково", "Перевірте витягнуті поля перед збереженням.");
        }
        else
        {
            ToastService.Success("Блок розібрано");
        }
    }

    private async Task SearchActionsAsync()
    {
        _lookupCts?.Cancel();
        _lookupCts?.Dispose();
        _lookupCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

        try
        {
            var result = await LookupService.SearchActionCatalogSuggestionsAsync(
                _actionSearch ?? string.Empty,
                10,
                _lookupCts.Token);

            _actionSuggestions = result.ToList();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SelectBoundAction(ActionCatalogSuggestionDto item)
    {
        _model.Draft.ObservationActionId = item.Id;
        _actionSearch = item.Name;
        _actionSuggestions.Clear();
    }

    private void ClearBoundAction()
    {
        _model.Draft.ObservationActionId = null;
        _actionSearch = null;
        _actionSuggestions.Clear();
    }

    private void ApplySuggestedAction()
    {
        if (!string.IsNullOrWhiteSpace(_model.SuggestedActionRaw))
            _model.Draft.ActionRaw = _model.SuggestedActionRaw;
    }

    private void AddTagRow() => _model.Draft.Tags.Add(new ObservationTagEditorRow());

    private void RemoveTag(ObservationTagEditorRow row) => _model.Draft.Tags.Remove(row);

    private void ApplySuggestedTag(ObservationRadioTagSuggestionRow suggestion)
    {
        if (_model.Draft.Tags.Any(x =>
                string.Equals(x.RawValue?.Trim(), suggestion.Value.Trim(), StringComparison.OrdinalIgnoreCase) &&
                x.Kind == suggestion.Kind))
        {
            suggestion.Applied = true;
            return;
        }

        _model.Draft.Tags.Add(new ObservationTagEditorRow
        {
            RawValue = suggestion.Value,
            Kind = suggestion.Kind,
            Source = ObservationTagSource.Derived
        });

        suggestion.Applied = true;
    }

    private void AddParticipantRow()
    {
        _model.Draft.Participants.Add(new ObservationParticipantEditorRow
        {
            Ordinal = GetNextOrdinal()
        });
    }

    private void RemoveParticipant(ObservationParticipantEditorRow row)
        => _model.Draft.Participants.Remove(row);

    private async Task SaveAsync()
    {
        if (!_model.IsParsed)
        {
            ToastService.Warning("Немає даних", "Спочатку розберіть вставлений блок.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_model.Draft.ActionRaw))
        {
            ToastService.Warning("Потрібна дія", "Вкажіть первинний текст дії.");
            return;
        }

        try
        {
            _saving = true;

            var request = BuildRequest();
            var result = await WriteService.CreateAsync(request, _lifetimeCts.Token);

            if (result.IsDuplicate)
            {
                ToastService.Warning("Дублікат", "Такий запис уже існує.");
                return;
            }

            ToastService.Success("Спостереження збережено");

            if (Saved.HasDelegate)
                await Saved.InvokeAsync(result.ObservationId);

            await CloseDrawerAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ToastService.Error("Не вдалося зберегти спостереження.", ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private ObservationUpsertRequestDto BuildRequest()
    {
        return new ObservationUpsertRequestDto
        {
            ObservedDate = _model.Draft.ObservedDate,
            ObservationActionId = _model.Draft.ObservationActionId,
            ActionRaw = _model.Draft.ActionRaw.Trim(),
            Layer = Clean(_model.Draft.Layer),
            RmRaw = Clean(_model.Draft.RmRaw),
            PointRaw = Clean(_model.Draft.PointRaw),
            LocationRaw = Clean(_model.Draft.LocationRaw),
            DistrictRaw = Clean(_model.Draft.DistrictRaw),
            SubdivisionRaw = Clean(_model.Draft.SubdivisionRaw),
            SubdivisionStrength = _model.Draft.SubdivisionStrength,
            SubdivisionSource = _model.Draft.SubdivisionSource,
            Note = Clean(_model.Draft.Note),
            Participants = _model.Draft.Participants
                .Where(x => !string.IsNullOrWhiteSpace(x.LabelRaw) || !string.IsNullOrWhiteSpace(x.RoleRaw) || x.IsUnknown)
                .Select(x => new ObservationParticipantUpsertDto
                {
                    LabelRaw = Clean(x.LabelRaw),
                    RoleRaw = Clean(x.RoleRaw),
                    IsUnknown = x.IsUnknown,
                    Ordinal = x.Ordinal
                })
                .ToList(),
            Tags = _model.Draft.Tags
                .Where(x => !string.IsNullOrWhiteSpace(x.RawValue))
                .Select(x => new ObservationTagUpsertDto
                {
                    RawValue = x.RawValue!.Trim(),
                    Kind = x.Kind,
                    Source = x.Source
                })
                .ToList(),
            ProbableActions = Array.Empty<ObservationProbableActionUpsertDto>()
        };
    }

    private int GetNextOrdinal()
        => _model.Draft.Participants.Count == 0 ? 1 : _model.Draft.Participants.Max(x => x.Ordinal) + 1;

    private static string GetTagKindText(TagKind value)
        => value switch
        {
            TagKind.Keyword => "Ключове",
            TagKind.Person => "Людина",
            TagKind.Location => "Місце",
            TagKind.Subdivision => "Підрозділ",
            TagKind.Callsign => "Позивний",
            _ => "Інше"
        };

    private async Task CloseDrawerAsync()
    {
        if (_drawer is not null)
        {
            await _drawer.CloseAsync();
            return;
        }

        await IsOpenChanged.InvokeAsync(false);
    }

    private async Task HandleDrawerClosedAsync()
    {
        _initialized = false;
        await IsOpenChanged.InvokeAsync(false);
    }

    public void Dispose()
    {
        _lookupCts?.Cancel();
        _lookupCts?.Dispose();
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
