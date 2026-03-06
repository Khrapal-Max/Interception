//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Observations.Abstractions;
using Interception.UI.Application.Observations.Dtos;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Observations.Drawers;

public partial class ObservationCreateDrawer : ComponentBase
{
    [Parameter] public bool IsOpen { get; set; }

    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Notify parent to refresh registry after successful create.</summary>
    [Parameter] public EventCallback OnCreated { get; set; }

    [Inject] public IObservationWriteService WriteService { get; set; } = default!;

    // form state
    private DateTime _date = DateTime.Today;
    private short _dayPart = 1;

    private string? _layer;
    private string _actionRaw = string.Empty;
    private string? _rmRaw;
    private string? _pointRaw;
    private string? _locationRaw;
    private string? _districtRaw;
    private string? _note;

    private readonly List<ParticipantDraft> _participants = [];

    private bool _saving;
    private string? _error;
    private bool _duplicate;

    protected override void OnParametersSet()
    {
        // When opened, reset transient UI messages (but keep typed data unless you want hard reset).
        if (IsOpen)
        {
            _error = null;
            _duplicate = false;
        }
    }

    private void AddParticipant()
    {
        _participants.Add(new ParticipantDraft());
    }

    private void RemoveParticipant(int index)
    {
        if (index < 0 || index >= _participants.Count) return;
        _participants.RemoveAt(index);
    }

    private async Task SaveAsync()
    {
        _error = null;
        _duplicate = false;

        if (string.IsNullOrWhiteSpace(_actionRaw))
        {
            _error = "Поле 'Дія' є обов'язковим.";
            return;
        }

        _saving = true;
        try
        {
            var participants = _participants
    .Select(p => new ObservationCreateParticipantDto(
        LabelRaw: p.LabelRaw,
        IsUnknown: p.IsUnknown,
        RoleRaw: p.RoleRaw))
    .ToList();

var req = new ObservationCreateRequestDto(

                ObservedDate: DateOnly.FromDateTime(_date),
                DayPart: _dayPart,
                ActionRaw: _actionRaw,
                Layer: _layer,
                RmRaw: _rmRaw,
                PointRaw: _pointRaw,
                LocationRaw: _locationRaw,
                DistrictRaw: _districtRaw,
                Note: _note,
                Participants: participants);

            var result = await WriteService.CreateAsync(req, CancellationToken.None);

            if (result.IsDuplicate)
            {
                _duplicate = true;
                return;
            }

            // success → close and notify parent
            if (OnCreated.HasDelegate)
                await OnCreated.InvokeAsync();

            if (OnClose.HasDelegate)
                await OnClose.InvokeAsync();

            // Optional: reset form after close
            ResetForm();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private void ResetForm()
    {
        _date = DateTime.Today;
        _dayPart = 1;

        _layer = null;
        _actionRaw = string.Empty;
        _rmRaw = null;
        _pointRaw = null;
        _locationRaw = null;
        _districtRaw = null;
        _note = null;

        _participants.Clear();
    }
}
