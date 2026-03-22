//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Interceptions.Drawers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Interception.Tests.Components.Interceptions;

public sealed class InterceptionFilterDrawerTests : BunitContext
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private IInterceptionService SetupService(
        IReadOnlyList<FrequencySuggestionDto>? freqSuggestions = null,
        IReadOnlyList<string>? vecSuggestions = null)
    {
        var svc = Substitute.For<IInterceptionService>();

        svc.GetFrequencyWithDivisionAsync(Arg.Any<string?>(), Arg.Any<int>(),
                                          Arg.Any<CancellationToken>())
           .Returns(freqSuggestions ?? []);

        svc.GetVectorSignalSuggestionsAsync(Arg.Any<string?>(), Arg.Any<string?>(),
                                            Arg.Any<int>(), Arg.Any<CancellationToken>())
           .Returns(vecSuggestions ?? []);

        Services.AddSingleton(svc);
        Services.AddSingleton<ToastService>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        return svc;
    }

    private IRenderedComponent<InterceptionFilterDrawer> RenderDrawer(
        InterceptionFilter? filter = null,
        bool isOpen = true,
        EventCallback<InterceptionFilter>? onApplied = null,
        EventCallback? onReset = null,
        EventCallback<bool>? isOpenChanged = null)
    {
        return Render<InterceptionFilterDrawer>(p => p
            .Add(d => d.IsOpen, isOpen)
            .Add(d => d.Filter, filter ?? new InterceptionFilter())
            .Add(d => d.OnApplied, onApplied ?? EventCallback<InterceptionFilter>.Empty)
            .Add(d => d.OnReset, onReset ?? EventCallback.Empty)
            .Add(d => d.IsOpenChanged, isOpenChanged ?? EventCallback<bool>.Empty));
    }

    // -------------------------------------------------------------------------
    // Початковий стан
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_OpenWithEmptyFilter_FieldsAreEmpty()
    {
        SetupService();

        var cut = RenderDrawer();

        // Всі текстові поля порожні
        foreach (var input in cut.FindAll("input[type=text]"))
            input.GetAttribute("value").Should().BeNullOrEmpty();
    }

    [Fact]
    public void Render_OpenWithExistingFilter_PreFillsFrequency()
    {
        SetupService();
        var filter = new InterceptionFilter { Frequency = "157.0250" };

        var cut = RenderDrawer(filter: filter);

        cut.FindAll("input[type=text]")
           .First(i => i.GetAttribute("class")?.Contains("font-monospace") == true)
           .GetAttribute("value").Should().Be("157.0250");
    }

    // -------------------------------------------------------------------------
    // Apply / Reset
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClickApply_InvokesOnAppliedWithModel()
    {
        SetupService();

        InterceptionFilter? applied = null;
        var onApplied = EventCallback.Factory.Create(
            this, (InterceptionFilter f) => applied = f);

        var cut = RenderDrawer(onApplied: onApplied);

        // Вводимо частоту
        cut.FindAll("input[type=text]")
           .First(i => i.GetAttribute("class")?.Contains("font-monospace") == true)
           .Input("157.0250");

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Застосувати")).Click());

        applied.Should().NotBeNull();
        applied!.Frequency.Should().Be("157.0250");
    }

    [Fact]
    public async Task ClickApply_ClosesDrawer()
    {
        SetupService();

        var isOpenChangedValue = true;
        var isOpenChanged = EventCallback.Factory.Create<bool>(
            this, v => isOpenChangedValue = v);

        var cut = RenderDrawer(isOpenChanged: isOpenChanged);

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Застосувати")).Click());

        isOpenChangedValue.Should().BeFalse();
    }

    [Fact]
    public async Task ClickReset_InvokesOnReset()
    {
        SetupService();

        var resetCalled = false;
        var onReset = EventCallback.Factory.Create(this, () => resetCalled = true);

        var cut = RenderDrawer(onReset: onReset);

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Скинути")).Click());

        resetCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ClickReset_ClosesDrawer()
    {
        SetupService();

        var isOpenChangedValue = true;
        var isOpenChanged = EventCallback.Factory.Create<bool>(
            this, v => isOpenChangedValue = v);

        var cut = RenderDrawer(isOpenChanged: isOpenChanged);

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Скинути")).Click());

        isOpenChangedValue.Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // Suggestions — частота
    // -------------------------------------------------------------------------

    [Fact]
    public void FrequencySuggestions_ShownOnInput()
    {
        SetupService(freqSuggestions:
        [
            new FrequencySuggestionDto { Frequency = "157.0250", Count = 5 },
            new FrequencySuggestionDto { Frequency = "410.1370", Count = 3 },
        ]);

        var cut = RenderDrawer();

        var freqInput = cut.FindAll("input[type=text]")
                           .First(i => i.GetAttribute("class")?.Contains("font-monospace") == true);
        freqInput.Input("15");

        // Список suggestions з'являється
        cut.FindAll(".autocomplete-item").Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SelectFrequencySuggestion_FillsFrequencyField()
    {
        SetupService(freqSuggestions:
        [
            new FrequencySuggestionDto { Frequency = "157.0250", Count = 5 }
        ]);

        InterceptionFilter? applied = null;
        var onApplied = EventCallback.Factory.Create(
            this, (InterceptionFilter f) => applied = f);

        var cut = RenderDrawer(onApplied: onApplied);

        var freqInput = cut.FindAll("input[type=text]")
                           .First(i => i.GetAttribute("class")?.Contains("font-monospace") == true);
        freqInput.Input("15");

        // Клікаємо перший suggestion
        cut.FindAll(".autocomplete-item")[0].MouseDown();

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Застосувати")).Click());

        applied!.Frequency.Should().Be("157.0250");
    }

    // -------------------------------------------------------------------------
    // Closed drawer
    // -------------------------------------------------------------------------

    [Fact]
    public void ClosedDrawer_FieldsNotPreFilled()
    {
        SetupService();
        var filter = new InterceptionFilter { Frequency = "157.0250" };

        var cut = RenderDrawer(filter: filter, isOpen: false);

        // Drawer закритий — _initialized = false — поля порожні
        foreach (var input in cut.FindAll("input[type=text]"))
            input.GetAttribute("value").Should().BeNullOrEmpty();
    }
}
