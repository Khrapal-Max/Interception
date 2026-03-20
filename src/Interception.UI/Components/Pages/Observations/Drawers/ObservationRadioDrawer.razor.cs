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
    [Parameter] public EventCallback<ObservationCreateSeedModel> ParsedToCreate { get; set; }

    [Inject] private IObservationLookupService LookupService { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;

    private Drawer? _drawer;
    private ObservationRadioDrawerModel _model = ObservationRadioDrawerModel.CreateEmpty();
    private bool _initialized;
    private bool _transferring;
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
            var rawText = _model.RawText;
            ToastService.Warning("Не вдалося розібрати блок", result.ErrorMessage ?? "Перевірте формат тексту.");
            _model = ObservationRadioDrawerModel.CreateEmpty();
            _model.RawText = rawText;
            return;
        }

        _model.IsParsed = true;
        _model.SourcePost = result.SourcePost;
        _model.SuggestedActionRaw = result.SuggestedActionRaw;
        _model.Seed = result.Seed;
        _model.SuggestedTags = result.SuggestedTags;
        _model.Warnings = result.Warnings;
        _actionSearch = _model.Seed.ActionRaw;

        if (_model.Warnings.Count > 0)
            ToastService.Warning("Блок розібрано частково", "Перевірте витягнуті поля перед передачею у форму.");
        else
            ToastService.Success("Блок розібрано");
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
        _model.Seed.ObservationActionId = item.Id;
        _model.Seed.ObservationActionName = item.Name;
        _actionSearch = item.Name;
        _actionSuggestions.Clear();
    }

    private void ClearBoundAction()
    {
        _model.Seed.ObservationActionId = null;
        _model.Seed.ObservationActionName = null;
        _actionSearch = null;
        _actionSuggestions.Clear();
    }

    private void ApplySuggestedAction()
    {
        if (!string.IsNullOrWhiteSpace(_model.SuggestedActionRaw))
            _model.Seed.ActionRaw = _model.SuggestedActionRaw;
    }

    private void AddTagRow() => _model.Seed.Tags.Add(new ObservationTagSeedRow());

    private void RemoveTag(ObservationTagSeedRow row) => _model.Seed.Tags.Remove(row);

    private void ApplySuggestedTag(ObservationRadioTagSuggestionRow suggestion)
    {
        if (_model.Seed.Tags.Any(x =>
                string.Equals(x.RawValue?.Trim(), suggestion.Value.Trim(), StringComparison.OrdinalIgnoreCase) &&
                x.Kind == suggestion.Kind))
        {
            suggestion.Applied = true;
            return;
        }

        _model.Seed.Tags.Add(new ObservationTagSeedRow
        {
            RawValue = suggestion.Value,
            Kind = suggestion.Kind,
            Source = ObservationTagSource.Derived
        });

        suggestion.Applied = true;
    }

    private void AddParticipantRow()
    {
        _model.Seed.Participants.Add(new ObservationParticipantSeedRow
        {
            Ordinal = GetNextOrdinal()
        });
    }

    private void RemoveParticipant(ObservationParticipantSeedRow row)
        => _model.Seed.Participants.Remove(row);

    private async Task TransferToCreateAsync()
    {
        if (!_model.IsParsed)
        {
            ToastService.Warning("Немає даних", "Спочатку розберіть вставлений блок.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_model.Seed.ActionRaw))
        {
            ToastService.Warning("Потрібна дія", "Вкажіть первинний текст дії.");
            return;
        }

        try
        {
            _transferring = true;

            if (!ParsedToCreate.HasDelegate)
            {
                ToastService.Warning("Немає обробника", "На сторінці не підключено передачу seed у форму створення.");
                return;
            }

            await ParsedToCreate.InvokeAsync(_model.Seed.Clone());
        }
        finally
        {
            _transferring = false;
        }
    }

    private int GetNextOrdinal()
        => _model.Seed.Participants.Count == 0 ? 1 : _model.Seed.Participants.Max(x => x.Ordinal) + 1;

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
}
