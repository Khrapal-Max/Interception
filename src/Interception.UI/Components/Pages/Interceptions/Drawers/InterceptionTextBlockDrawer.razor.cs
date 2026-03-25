//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Interception.UI.Application.Interceptions.Abstractions.Registry;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Interceptions.TextBlock;
using Interception.UI.Application.Toasts;
using Interception.UI.Domain;
using Interception.UI.Extensions;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Pages.Interceptions.Drawers;

public partial class InterceptionTextBlockDrawer : ComponentBase
{
    [Inject] private IInterceptionCommandService InterceptionCommandService { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
    [Parameter] public IReadOnlyList<InterceptionAction> Actions { get; set; } = [];
    [Parameter] public EventCallback OnSaved { get; set; }

    // -------------------------------------------------------------------------
    // Стан
    // -------------------------------------------------------------------------

    private string? _rawText = null;
    private string? _parseError = null;
    private TextBlockParseResult? _parsed = null;
    private InterceptionFormDto? _form = null;
    private bool _saving;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void OnDrawerClosed() => ResetState();

    // -------------------------------------------------------------------------
    // Крок 1 — парсинг
    // -------------------------------------------------------------------------

    private void ParseAsync()
    {
        _parseError = null;
        var result = TextBlockParser.Parse(_rawText);

        if (!result.IsSuccess)
        {
            _parseError = result.Error;
            return;
        }

        _parsed = result;
        _form = BuildForm(result);
    }

    private static InterceptionFormDto BuildForm(TextBlockParseResult r)
    {
        var ordinal = 1;
        var participants = new List<ParticipantFormDto>
        {
            // Ініціатор
            new() {
                Ordinal = ordinal++,
                Name = r.Initiator,
                IsUnknown = r.Initiator is null
            }
        };

        // Відповідачі
        foreach (var name in r.Responders)
        {
            participants.Add(new ParticipantFormDto
            {
                Ordinal = ordinal++,
                Name = name,
                IsUnknown = name is null
            });
        }

        // Якщо нікого не розпізнали — два порожніх НВ
        if (participants.Count == 0)
        {
            participants.Add(new ParticipantFormDto { Ordinal = 1, IsUnknown = true });
            participants.Add(new ParticipantFormDto { Ordinal = 2, IsUnknown = true });
        }

        var observedDate = r.ObservedDate.HasValue
            ? DateTimeConverter.ToUtc(r.ObservedDate.Value)
            : DateTimeConverter.ToDisplay(DateTimeConverter.Now);

        return new InterceptionFormDto
        {
            ObservedDate = observedDate,
            Frequency = r.Frequency,
            Division = r.Division,
            VectorSignal = r.VectorSignal,
            Note = r.Note,
            InterceptionActionId = Guid.Empty,   // оператор обирає вручну
            Participants = participants,
        };
    }

    // -------------------------------------------------------------------------
    // Крок 2 — редагування форми
    // -------------------------------------------------------------------------

    private void OnObservedDateChange(ChangeEventArgs e)
    {
        if (_form is null) return;
        var parsed = DateTimeConverter.Parse(e.Value?.ToString());
        if (parsed.HasValue) _form.ObservedDate = parsed.Value;
    }

    private void OnActionChange(ChangeEventArgs e)
    {
        if (_form is null) return;
        if (Guid.TryParse(e.Value?.ToString(), out var id))
            _form.InterceptionActionId = id;
    }

    private static void OnUnknownToggle(ParticipantFormDto p, bool isUnknown)
    {
        p.IsUnknown = isUnknown;
        if (isUnknown) p.Name = null;
    }

    private void AddParticipant()
    {
        if (_form is null) return;
        var next = _form.Participants.Count == 0
            ? 1
            : _form.Participants.Max(p => p.Ordinal) + 1;
        _form.Participants.Add(new ParticipantFormDto { Ordinal = next, IsUnknown = true });
    }

    // -------------------------------------------------------------------------
    // Збереження
    // -------------------------------------------------------------------------

    private async Task SaveAsync()
    {
        if (_form is null) return;

        _saving = true;
        try
        {
            await InterceptionCommandService.CreateAsync(_form, "operator");
            Toasts.Success("Збережено", $"Запис від {_form.ObservedDate:dd.MM HH:mm} створено.");

            // Явно скидаємо стан — готуємо дравер до наступного запису
            ResetState();

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
        }
    }

    private void ResetState()
    {
        _rawText = null;
        _parseError = null;
        _parsed = null;
        _form = null;
    }

    private async Task CloseAsync()
        => await IsOpenChanged.InvokeAsync(false);
}
