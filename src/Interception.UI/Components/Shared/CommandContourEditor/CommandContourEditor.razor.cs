using Interception.UI.Application.Registry.Dtos;
using Microsoft.AspNetCore.Components;

namespace Interception.UI.Components.Shared.CommandContourEditor;

public partial class CommandContourEditor : ComponentBase
{
    [Parameter] public string IdPrefix { get; set; } = "common";
    [Parameter] public string? HintText { get; set; }
    [Parameter] public bool Enabled { get; set; }
    [Parameter] public EventCallback<bool> EnabledChanged { get; set; }
    [Parameter] public bool Loading { get; set; }
    [Parameter] public bool Saving { get; set; }
    [Parameter] public IReadOnlyList<CommandContourOptionDto> Options { get; set; } = [];
    [Parameter] public CommandContourSaveDto Form { get; set; } = new();
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

    private static string BuildOptionValue(CommandContourOptionDto option)
        => $"{(option.IsCanonicalPerson ? "canonical" : "resolved")}:{option.IdentityId}";

    private static string BuildOptionLabel(CommandContourOptionDto option)
        => $"{option.DisplayName} [{option.KindLabel}]";

    private static string BuildRelationTypeLabel(CommandContourRelationTypeDto relationType)
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

    private static string BuildConfidenceLabel(CommandContourConfidenceDto confidence)
        => confidence switch
        {
            CommandContourConfidenceDto.Low => "Низька",
            CommandContourConfidenceDto.Medium => "Середня",
            CommandContourConfidenceDto.High => "Висока",
            _ => confidence.ToString()
        };
}
