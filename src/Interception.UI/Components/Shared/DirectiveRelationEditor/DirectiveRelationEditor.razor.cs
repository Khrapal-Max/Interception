using Interception.UI.Application.Interceptions.Dtos;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Shared.DirectiveRelationEditor;

public partial class DirectiveRelationEditor : ComponentBase
{
    [Parameter] public string IdPrefix { get; set; } = "common";
    [Parameter] public string? HintText { get; set; }
    [Parameter] public bool Enabled { get; set; }
    [Parameter] public EventCallback<bool> EnabledChanged { get; set; }
    [Parameter] public bool Loading { get; set; }
    [Parameter] public bool Saving { get; set; }
    [Parameter] public IReadOnlyList<PersonDirectiveRelationOptionDto> Options { get; set; } = [];
    [Parameter] public PersonDirectiveRelationSaveDto Form { get; set; } = new();
    [Parameter] public string? FromSelection { get; set; }
    [Parameter] public EventCallback<string?> FromSelectionChanged { get; set; }
    [Parameter] public string? ToSelection { get; set; }
    [Parameter] public EventCallback<string?> ToSelectionChanged { get; set; }
    [Parameter] public EventCallback OnRefreshOptions { get; set; }

    private Task OnEnabledChanged(ChangeEventArgs args)
    {
        var value = args.Value is bool flag && flag;
        return EnabledChanged.InvokeAsync(value);
    }

    private Task RefreshOptions() => OnRefreshOptions.InvokeAsync();

    private Task OnFromSelectionChanged(ChangeEventArgs args)
        => FromSelectionChanged.InvokeAsync(args.Value?.ToString());

    private Task OnToSelectionChanged(ChangeEventArgs args)
        => ToSelectionChanged.InvokeAsync(args.Value?.ToString());

    private static string BuildOptionValue(PersonDirectiveRelationOptionDto option)
        => $"{(option.IsCanonicalPerson ? "canonical" : "resolved")}:{option.IdentityId}";

    private static string BuildOptionLabel(PersonDirectiveRelationOptionDto option)
        => $"{option.DisplayName} [{option.KindLabel}]";

    private static string BuildRelationTypeLabel(DirectiveRelationTypeDto relationType)
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

    private static string BuildConfidenceLabel(DirectiveRelationConfidenceDto confidence)
        => confidence switch
        {
            DirectiveRelationConfidenceDto.Low => "Низька",
            DirectiveRelationConfidenceDto.Medium => "Середня",
            DirectiveRelationConfidenceDto.High => "Висока",
            _ => confidence.ToString()
        };
}
