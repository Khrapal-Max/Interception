//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Registry.InterceptionActions.Drawers;
using Interception.UI.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Interception.Tests.Components.InterceptionActions;

public sealed class ActionFormDrawerTests : BunitContext
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private IInterceptionActionService SetupService()
    {
        var svc = Substitute.For<IInterceptionActionService>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton(svc);
        Services.AddSingleton<ToastService>();

        return svc;
    }

    private IRenderedComponent<ActionFormDrawer> RenderDrawer(
        InterceptionAction? editingAction = null,
        bool isOpen = true,
        EventCallback? onSaved = null,
        EventCallback<bool>? isOpenChanged = null)
    {
        return Render<ActionFormDrawer>(p => p
            .Add(d => d.IsOpen, isOpen)
            .Add(d => d.EditingAction, editingAction)
            .Add(d => d.OnSaved, onSaved ?? EventCallback.Empty)
            .Add(d => d.IsOpenChanged, isOpenChanged ?? EventCallback<bool>.Empty));
    }

    // -------------------------------------------------------------------------
    // Режим створення — початковий стан
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateMode_EditingAction_IsNull()
    {
        SetupService();

        var cut = RenderDrawer();

        cut.Instance.EditingAction.Should().BeNull();
    }

    [Fact]
    public void CreateMode_NameInput_IsEmpty()
    {
        SetupService();

        var cut = RenderDrawer();

        cut.Find("input[type=text]")
           .GetAttribute("value").Should().BeNullOrEmpty();
    }

    [Fact]
    public void CreateMode_Description_IsEmpty()
    {
        SetupService();

        var cut = RenderDrawer();

        cut.Find("textarea")
           .GetAttribute("value").Should().BeNullOrEmpty();
    }

    // -------------------------------------------------------------------------
    // Режим редагування — передзаповнення
    // -------------------------------------------------------------------------

    [Fact]
    public void EditMode_PreFillsName()
    {
        SetupService();
        var action = InterceptionAction.Create("координація дій", "опис дії");

        var cut = RenderDrawer(editingAction: action);

        cut.Find("input[type=text]")
           .GetAttribute("value").Should().Be("координація дій");
    }

    [Fact]
    public void EditMode_PreFillsDescription()
    {
        SetupService();
        var action = InterceptionAction.Create("координація дій", "опис дії");

        var cut = RenderDrawer(editingAction: action);

        cut.Find("textarea")
           .GetAttribute("value").Should().Be("опис дії");
    }

    // -------------------------------------------------------------------------
    // Валідація порожньої назви
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Save_EmptyName_ShowsInvalidClass()
    {
        SetupService();

        var cut = RenderDrawer();

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Створити")).Click());

        cut.Find("input[type=text]")
           .ClassList.Should().Contain("is-invalid");
    }

    [Fact]
    public async Task Save_EmptyName_ShowsValidationMessage()
    {
        SetupService();

        var cut = RenderDrawer();

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Створити")).Click());

        cut.Find(".invalid-feedback")
           .TextContent.Should().Contain("обов'язкова");
    }

    [Fact]
    public async Task Save_EmptyName_DoesNotCallService()
    {
        var svc = SetupService();

        var cut = RenderDrawer();

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Створити")).Click());

        await svc.DidNotReceive()
                 .CreateAsync(Arg.Any<string>(), Arg.Any<string>(),
                              Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NameInput_AfterValidationError_ClearsError()
    {
        SetupService();

        var cut = RenderDrawer();

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Створити")).Click());

        cut.Find("input[type=text]").Input("нова назва");

        cut.FindAll(".invalid-feedback").Should().BeEmpty();
        cut.Find("input[type=text]").ClassList.Should().NotContain("is-invalid");
    }

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Save_ValidName_CreateMode_CallsCreateAsync()
    {
        var svc = SetupService();
        svc.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(InterceptionAction.Create("координація дій", ""));

        var cut = RenderDrawer();

        cut.Find("input[type=text]").Input("координація дій");
        cut.Find("textarea").Input("опис");

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Створити")).Click());

        await svc.Received(1)
                 .CreateAsync("координація дій", "опис", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_ValidName_CreateMode_InvokesOnSaved()
    {
        var svc = SetupService();
        svc.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(InterceptionAction.Create("нова дія", ""));

        var onSavedCalled = false;
        var onSaved = EventCallback.Factory.Create(this, () => onSavedCalled = true);

        var cut = RenderDrawer(onSaved: onSaved);
        cut.Find("input[type=text]").Input("нова дія");

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Створити")).Click());

        onSavedCalled.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Save_EditMode_CallsUpdateAsync()
    {
        var svc = SetupService();
        var action = InterceptionAction.Create("стара назва", "старий опис");

        svc.UpdateAsync(action.Id, Arg.Any<string>(), Arg.Any<string>(),
                        Arg.Any<CancellationToken>())
           .Returns(action);

        var cut = RenderDrawer(editingAction: action);

        cut.Find("input[type=text]").Input("нова назва");

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Зберегти")).Click());

        await svc.Received(1)
                 .UpdateAsync(action.Id, "нова назва", Arg.Any<string>(),
                              Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Серверна помилка (дублікат)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Save_DuplicateName_ShowsAlertDanger()
    {
        var svc = SetupService();
        svc.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .ThrowsAsync(new InvalidOperationException("вже існує в довіднику"));

        var cut = RenderDrawer();
        cut.Find("input[type=text]").Input("координація дій");

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Створити")).Click());

        cut.Find(".alert-danger")
           .TextContent.Should().Contain("вже існує в довіднику");
    }

    [Fact]
    public async Task Save_DuplicateName_DrawerRemainsOpen()
    {
        var svc = SetupService();
        svc.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .ThrowsAsync(new InvalidOperationException("вже існує в довіднику"));

        var isOpenChangedValue = true;
        var isOpenChanged = EventCallback.Factory.Create<bool>(
            this, v => isOpenChangedValue = v);

        var cut = RenderDrawer(isOpenChanged: isOpenChanged);
        cut.Find("input[type=text]").Input("координація дій");

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Створити")).Click());

        isOpenChangedValue.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Лічильники символів
    // -------------------------------------------------------------------------

    [Fact]
    public void CharCounter_Name_UpdatesOnInput()
    {
        SetupService();

        var cut = RenderDrawer();
        cut.Find("input[type=text]").Input("привіт");

        cut.Markup.Should().Contain("6 / 100");
    }

    [Fact]
    public void CharCounter_Description_UpdatesOnInput()
    {
        SetupService();

        var cut = RenderDrawer();
        cut.Find("textarea").Input("тест");

        cut.Markup.Should().Contain("4 / 500");
    }

    // -------------------------------------------------------------------------
    // Закритий дравер
    // -------------------------------------------------------------------------

    [Fact]
    public void ClosedDrawer_FieldsNotPreFilled()
    {
        SetupService();
        var action = InterceptionAction.Create("назва", "опис");

        var cut = RenderDrawer(editingAction: action, isOpen: false);

        // Drawer закритий → _initialized = false → OnParametersSet не заповнює поля.
        // Поля є в DOM (Drawer рендерить HTML і при isOpen=false),
        // але їхні значення порожні — дані не підставились.
        cut.Find("input[type=text]")
           .GetAttribute("value").Should().BeNullOrEmpty();

        cut.Find("textarea")
           .GetAttribute("value").Should().BeNullOrEmpty();
    }
}
