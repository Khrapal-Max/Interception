//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using System.Text.RegularExpressions;
using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Observations.Models;
using Interception.UI.Components.Shared.Drawer;
using Interception.UI.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Observations.Drawers;

public partial class ObservationCreateDrawer : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _lifetimeCts = new();
    private CancellationTokenSource? _actionLookupCts;
    private CancellationTokenSource? _participantLookupCts;
    private CancellationTokenSource? _probableLookupCts;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public Guid? ObservationId { get; set; }
    [Parameter] public ObservationDetailsDto? Details { get; set; }
    [Parameter] public ObservationCreateSeedModel? Seed { get; set; }
    [Parameter] public EventCallback<Guid> Saved { get; set; }

    [Inject] private IObservationWriteService WriteService { get; set; } = default!;
    [Inject] private IObservationLookupService LookupService { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;

    private Drawer? _drawer;
    private ObservationEditorModel _model = CreateEmptyModel();
    private bool _saving;
    private bool _initialized;
    private bool _seedApplied;
    private bool _seedNeedsParticipantEnrichment;
    private string? _appliedSeedKey;
    private List<ActionCatalogSuggestionDto> _actionSuggestions = [];
    private List<ParticipantSuggestionDto> _participantSuggestions = [];
    private List<ActionCatalogSuggestionDto> _probableActionSuggestions = [];
    private string? _actionSearch;
    private string? _participantSearch;
    private string? _probableActionSearch;

    private readonly IReadOnlyList<SelectOptionModel> _subdivisionSourceOptions =
    [
        new(ObservationSubdivisionSource.Manual.ToString(), "Вручну"),
        new(ObservationSubdivisionSource.Layer.ToString(), "Шар"),
        new(ObservationSubdivisionSource.Rm.ToString(), "Р/М"),
        new(ObservationSubdivisionSource.Note.ToString(), "Примітка"),
        new(ObservationSubdivisionSource.Derived.ToString(), "Похідне"),
        new(ObservationSubdivisionSource.Import.ToString(), "Імпорт")
    ];

    private readonly IReadOnlyList<SelectOptionModel> _tagKindOptions =
    [
        new(TagKind.Keyword.ToString(), "Ключове"),
        new(TagKind.Person.ToString(), "Людина"),
        new(TagKind.Location.ToString(), "Місце"),
        new(TagKind.Subdivision.ToString(), "Підрозділ"),
        new(TagKind.Callsign.ToString(), "Позивний"),
        new(TagKind.Other.ToString(), "Інше")
    ];

    private readonly IReadOnlyList<SelectOptionModel> _tagSourceOptions =
    [
        new(ObservationTagSource.Manual.ToString(), "Вручну"),
        new(ObservationTagSource.Import.ToString(), "Імпорт"),
        new(ObservationTagSource.Derived.ToString(), "Похідне")
    ];

    private readonly IReadOnlyList<SelectOptionModel> _probableSourceOptions =
    [
        new(ProbableActionSource.Manual.ToString(), "Вручну"),
        new(ProbableActionSource.Rule.ToString(), "Правило"),
        new(ProbableActionSource.Derived.ToString(), "Похідне")
    ];

    protected override void OnParametersSet()
    {
        if (!IsOpen)
        {
            _initialized = false;
            _seedApplied = false;
            _seedNeedsParticipantEnrichment = false;
            _appliedSeedKey = null;
            return;
        }

        if (!_initialized)
        {
            _initialized = true;
            _seedApplied = false;
            _appliedSeedKey = null;
            _actionSuggestions.Clear();
            _participantSuggestions.Clear();
            _probableActionSuggestions.Clear();
            _actionSearch = null;
            _participantSearch = null;
            _probableActionSearch = null;

            _model = BuildModel(Details);
        }

        if (ObservationId is null && Seed is not null)
        {
            var seedKey = BuildSeedKey(Seed);
            if (!string.Equals(seedKey, _appliedSeedKey, StringComparison.Ordinal))
            {
                _model = CreateEmptyModel();
                ApplySeed(_model, Seed);
                _seedApplied = true;
                _seedNeedsParticipantEnrichment = _model.Participants.Count > 0;
                _appliedSeedKey = seedKey;
                _actionSearch = _model.ObservationActionName ?? _model.ActionRaw;
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_seedNeedsParticipantEnrichment)
        {
            _seedNeedsParticipantEnrichment = false;
            await EnrichParticipantsAsync();
            StateHasChanged();
        }
    }

    private async Task SearchActionsAsync()
    {
        _actionLookupCts?.Cancel();
        _actionLookupCts?.Dispose();
        _actionLookupCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

        try
        {
            _actionSuggestions = [.. (await LookupService.SearchActionCatalogSuggestionsAsync(_actionSearch ?? string.Empty, 10, _actionLookupCts.Token))];
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SearchParticipantsAsync()
    {
        _participantLookupCts?.Cancel();
        _participantLookupCts?.Dispose();
        _participantLookupCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

        try
        {
            _participantSuggestions = [.. (await LookupService.SearchParticipantSuggestionsAsync(_participantSearch ?? string.Empty, 10, _participantLookupCts.Token))];
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SearchProbableActionsAsync()
    {
        _probableLookupCts?.Cancel();
        _probableLookupCts?.Dispose();
        _probableLookupCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

        try
        {
            _probableActionSuggestions = [.. (await LookupService.SearchActionCatalogSuggestionsAsync(_probableActionSearch ?? string.Empty, 10, _probableLookupCts.Token))];
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SelectBoundAction(ActionCatalogSuggestionDto item)
    {
        _model.ObservationActionId = item.Id;
        _model.ObservationActionName = item.Name;
        _actionSearch = item.Name;
        _actionSuggestions.Clear();
    }

    private void ClearBoundAction()
    {
        _model.ObservationActionId = null;
        _model.ObservationActionName = null;
        _actionSearch = null;
    }

    private void AddParticipantRow()
        => _model.Participants.Add(new ObservationParticipantEditorRow { Ordinal = GetNextOrdinal() });

    private void AddUnknownParticipant()
        => _model.Participants.Add(new ObservationParticipantEditorRow
        {
            LabelRaw = "НВ",
            IsUnknown = true,
            Ordinal = GetNextOrdinal()
        });

    private void AddSuggestedParticipant(ParticipantSuggestionDto item)
    {
        _model.Participants.Add(new ObservationParticipantEditorRow
        {
            LabelRaw = item.LabelRaw,
            RoleRaw = item.PrimaryRole,
            IsUnknown = false,
            Ordinal = GetNextOrdinal(),
            SuggestedKnownLabel = item.LabelRaw,
            SuggestedKnownRole = item.PrimaryRole,
            SuggestedKnownSeenCount = item.SeenCount,
            KnownLookupChecked = true,
            KnownSuggestionApplied = true
        });

        _participantSearch = item.LabelRaw;
        _participantSuggestions.Clear();
    }

    private void RemoveParticipant(ObservationParticipantEditorRow row) => _model.Participants.Remove(row);

    private void AddTagRow() => _model.Tags.Add(new ObservationTagEditorRow());

    private void RemoveTag(ObservationTagEditorRow row) => _model.Tags.Remove(row);

    private void AddProbableAction(ActionCatalogSuggestionDto item)
    {
        if (_model.ProbableActions.Any(x => x.ObservationActionId == item.Id))
            return;

        _model.ProbableActions.Add(new ObservationProbableActionEditorRow
        {
            ObservationActionId = item.Id,
            ObservationActionName = item.Name,
            Confidence = 0.50m,
            Source = ProbableActionSource.Manual
        });

        _probableActionSearch = item.Name;
        _probableActionSuggestions.Clear();
    }

    private void RemoveProbableAction(ObservationProbableActionEditorRow row) => _model.ProbableActions.Remove(row);

    private async Task EnrichParticipantsAsync()
    {
        foreach (var row in _model.Participants.OrderBy(x => x.Ordinal))
        {
            await RefreshKnownSuggestionAsync(row);
        }
    }

    private async Task RefreshKnownSuggestionAsync(ObservationParticipantEditorRow row)
    {
        if (row.IsUnknown || string.IsNullOrWhiteSpace(row.LabelRaw))
        {
            row.ClearKnownSuggestion();
            return;
        }

        try
        {
            var suggestions = await LookupService.SearchParticipantSuggestionsAsync(
                row.LabelRaw.Trim(),
                5,
                _lifetimeCts.Token);

            row.KnownLookupChecked = true;
            var match = SelectKnownSuggestion(row.LabelRaw, suggestions);

            if (match is null)
            {
                row.SuggestedKnownLabel = null;
                row.SuggestedKnownRole = null;
                row.SuggestedKnownSeenCount = 0;
                row.KnownSuggestionApplied = false;
                return;
            }

            row.SuggestedKnownLabel = match.LabelRaw;
            row.SuggestedKnownRole = match.PrimaryRole;
            row.SuggestedKnownSeenCount = match.SeenCount;
            row.KnownSuggestionApplied = false;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ApplyKnownSuggestion(ObservationParticipantEditorRow row)
    {
        if (!row.HasKnownSuggestion)
            return;

        row.LabelRaw = row.SuggestedKnownLabel;
        if (string.IsNullOrWhiteSpace(row.RoleRaw))
            row.RoleRaw = row.SuggestedKnownRole;

        row.IsUnknown = false;
        row.KnownSuggestionApplied = true;
    }

    private async Task SaveAsync()
    {
        try
        {
            _saving = true;
            var request = BuildRequest();
            var result = ObservationId is null
                ? await WriteService.CreateAsync(request, _lifetimeCts.Token)
                : await WriteService.UpdateAsync(ObservationId.Value, request, _lifetimeCts.Token);

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
        _seedApplied = false;
        _appliedSeedKey = null;
        await IsOpenChanged.InvokeAsync(false);
    }

    private ObservationUpsertRequestDto BuildRequest()
    {
        if (string.IsNullOrWhiteSpace(_model.ActionRaw))
            throw new InvalidOperationException("Потрібно вказати дію.");

        return new ObservationUpsertRequestDto
        {
            ObservedDate = _model.ObservedDate,
            ObservationActionId = _model.ObservationActionId,
            ActionRaw = _model.ActionRaw.Trim(),
            Layer = Clean(_model.Layer),
            RmRaw = Clean(_model.RmRaw),
            PointRaw = Clean(_model.PointRaw),
            LocationRaw = Clean(_model.LocationRaw),
            DistrictRaw = Clean(_model.DistrictRaw),
            SubdivisionRaw = Clean(_model.SubdivisionRaw),
            SubdivisionStrength = _model.SubdivisionStrength,
            SubdivisionSource = _model.SubdivisionSource,
            Note = Clean(_model.Note),
            Participants = [.. _model.Participants
                .Where(x => !string.IsNullOrWhiteSpace(x.LabelRaw) || !string.IsNullOrWhiteSpace(x.RoleRaw) || x.IsUnknown)
                .Select(x => new ObservationParticipantUpsertDto
                {
                    Id = x.Id,
                    LabelRaw = Clean(x.LabelRaw),
                    IsUnknown = x.IsUnknown,
                    RoleRaw = Clean(x.RoleRaw),
                    Ordinal = x.Ordinal
                })],
            Tags = [.. _model.Tags
                .Where(x => !string.IsNullOrWhiteSpace(x.RawValue))
                .Select(x => new ObservationTagUpsertDto
                {
                    Id = x.Id,
                    RawValue = x.RawValue!.Trim(),
                    Kind = x.Kind,
                    Source = x.Source
                })],
            ProbableActions = [.. _model.ProbableActions
                .Select(x => new ObservationProbableActionUpsertDto
                {
                    Id = x.Id,
                    ObservationActionId = x.ObservationActionId,
                    Confidence = x.Confidence,
                    Reason = Clean(x.Reason),
                    Source = x.Source
                })]
        };
    }

    private int GetNextOrdinal()
        => _model.Participants.Count == 0 ? 1 : _model.Participants.Max(x => x.Ordinal) + 1;

    private static ObservationEditorModel BuildModel(ObservationDetailsDto? details)
    {
        if (details is null)
            return CreateEmptyModel();

        return new ObservationEditorModel
        {
            ObservedDate = details.ObservedDate,
            ObservationActionId = details.ObservationActionId,
            ObservationActionName = details.ObservationActionName,
            ActionRaw = details.ActionRaw,
            Layer = details.Layer,
            RmRaw = details.RmRaw,
            PointRaw = details.PointRaw,
            LocationRaw = details.LocationRaw,
            DistrictRaw = details.DistrictRaw,
            SubdivisionRaw = details.SubdivisionRaw,
            SubdivisionStrength = details.SubdivisionStrength,
            SubdivisionSource = details.SubdivisionSource ?? ObservationSubdivisionSource.Manual,
            Note = details.Note,
            Participants = [.. details.Participants
                .OrderBy(x => x.Ordinal)
                .Select(x => new ObservationParticipantEditorRow
                {
                    Id = x.Id,
                    LabelRaw = x.LabelRaw,
                    RoleRaw = x.RoleRaw,
                    IsUnknown = x.IsUnknown,
                    Ordinal = x.Ordinal
                })],
            Tags = [.. details.Tags
                .Select(x => new ObservationTagEditorRow
                {
                    Id = x.Id,
                    RawValue = x.RawValue,
                    Kind = x.Kind,
                    Source = x.Source
                })],
            ProbableActions = [.. details.ProbableActions
                .Select(x => new ObservationProbableActionEditorRow
                {
                    Id = x.Id,
                    ObservationActionId = x.ObservationActionId,
                    ObservationActionName = x.ObservationActionName,
                    Confidence = x.Confidence,
                    Reason = x.Reason,
                    Source = x.Source
                })]
        };
    }

    private static ObservationEditorModel CreateEmptyModel()
        => new() { ObservedDate = DateTime.Now, SubdivisionSource = ObservationSubdivisionSource.Manual };

    private static string BuildSeedKey(ObservationCreateSeedModel seed)
    {
        var participantKey = string.Join('|', seed.Participants
            .OrderBy(x => x.Ordinal)
            .Select(x => $"{x.Ordinal}:{x.IsUnknown}:{x.LabelRaw}:{x.RoleRaw}"));

        var tagKey = string.Join('|', seed.Tags
            .OrderBy(x => x.RawValue)
            .Select(x => $"{x.Kind}:{x.Source}:{x.RawValue}"));

        return string.Join("||",
            seed.ObservedDate.ToString("O"),
            seed.ObservationActionId?.ToString() ?? string.Empty,
            seed.ObservationActionName ?? string.Empty,
            seed.ActionRaw ?? string.Empty,
            seed.Layer ?? string.Empty,
            seed.RmRaw ?? string.Empty,
            seed.PointRaw ?? string.Empty,
            seed.LocationRaw ?? string.Empty,
            seed.DistrictRaw ?? string.Empty,
            seed.SubdivisionRaw ?? string.Empty,
            seed.SubdivisionStrength?.ToString() ?? string.Empty,
            seed.SubdivisionSource.ToString(),
            seed.Note ?? string.Empty,
            participantKey,
            tagKey);
    }

    private static void ApplySeed(ObservationEditorModel model, ObservationCreateSeedModel seed)
    {
        model.ObservedDate = seed.ObservedDate;
        model.ObservationActionId = seed.ObservationActionId;
        model.ObservationActionName = seed.ObservationActionName;
        model.ActionRaw = seed.ActionRaw;
        model.Layer = seed.Layer;
        model.RmRaw = seed.RmRaw;
        model.PointRaw = seed.PointRaw;
        model.LocationRaw = seed.LocationRaw;
        model.DistrictRaw = seed.DistrictRaw;
        model.SubdivisionRaw = seed.SubdivisionRaw;
        model.SubdivisionStrength = seed.SubdivisionStrength;
        model.SubdivisionSource = seed.SubdivisionSource;
        model.Note = seed.Note;
        model.Participants = seed.Participants
            .OrderBy(x => x.Ordinal)
            .Select(x => new ObservationParticipantEditorRow
            {
                LabelRaw = x.LabelRaw,
                RoleRaw = x.RoleRaw,
                IsUnknown = x.IsUnknown,
                Ordinal = x.Ordinal
            })
            .ToList();
        model.Tags = seed.Tags
            .Select(x => new ObservationTagEditorRow
            {
                RawValue = x.RawValue,
                Kind = x.Kind,
                Source = x.Source
            })
            .ToList();
    }

    private static ParticipantSuggestionDto? SelectKnownSuggestion(string? label, IReadOnlyList<ParticipantSuggestionDto> suggestions)
    {
        var normalizedLabel = NormalizeParticipant(label);
        if (string.IsNullOrWhiteSpace(normalizedLabel))
            return null;

        var exactMatches = suggestions
            .Where(x =>
                NormalizeParticipant(x.LabelRaw) == normalizedLabel ||
                NormalizeParticipant(x.LabelNorm) == normalizedLabel)
            .ToList();

        if (exactMatches.Count == 1)
            return exactMatches[0];

        return null;
    }

    private static string NormalizeParticipant(string? value)
        => Regex.Replace(value ?? string.Empty, @"[\s\p{P}\p{S}_]+", string.Empty)
            .Trim()
            .ToUpperInvariant();

    private static string GetActionCategoryText(ObservationActionCategory value)
        => value switch
        {
            ObservationActionCategory.Communication => "Комунікація",
            ObservationActionCategory.Movement => "Рух",
            ObservationActionCategory.Fire => "Вогонь",
            ObservationActionCategory.Command => "Управління",
            ObservationActionCategory.Recon => "Розвідка",
            ObservationActionCategory.Logistics => "Логістика",
            ObservationActionCategory.Support => "Підтримка",
            _ => "Інше"
        };

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void Dispose()
    {
        _actionLookupCts?.Cancel();
        _actionLookupCts?.Dispose();
        _participantLookupCts?.Cancel();
        _participantLookupCts?.Dispose();
        _probableLookupCts?.Cancel();
        _probableLookupCts?.Dispose();
        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();
        GC.SuppressFinalize(this);
    }
}
