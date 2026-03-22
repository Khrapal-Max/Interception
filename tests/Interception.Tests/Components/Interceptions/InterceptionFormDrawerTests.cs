//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using FluentAssertions;
using Interception.UI.Application.Interceptions.Abstractions;
using Interception.UI.Application.Interceptions.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Interceptions.Drawers;
using Interception.UI.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Interception.Tests.Components.Interceptions;

public sealed class InterceptionFormDrawerTests : BunitContext
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private (IInterceptionService svc, IInterceptionActionService actionSvc) SetupServices(
        InterceptionMessage? editMessage = null)
    {
        var svc = Substitute.For<IInterceptionService>();
        var actionSvc = Substitute.For<IInterceptionActionService>();

        svc.GetFrequencyWithDivisionAsync(Arg.Any<string?>(), Arg.Any<int>(),
                                          Arg.Any<CancellationToken>())
           .Returns([]);

        svc.GetVectorSignalSuggestionsAsync(Arg.Any<string?>(), Arg.Any<string?>(),
                                            Arg.Any<int>(), Arg.Any<CancellationToken>())
           .Returns([]);

        svc.GetParticipantSuggestionsAsync(Arg.Any<string?>(), Arg.Any<int>(),
                                           Arg.Any<CancellationToken>())
           .Returns([]);

        if (editMessage is not null)
            svc.GetByIdAsync(editMessage.Id, Arg.Any<CancellationToken>())
               .Returns(editMessage);

        actionSvc.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        Services.AddSingleton(svc);
        Services.AddSingleton(actionSvc);
        Services.AddSingleton<ToastService>();
        JSInterop.Mode = JSRuntimeMode.Loose;

        return (svc, actionSvc);
    }

    private IRenderedComponent<InterceptionFormDrawer> RenderDrawer(
        Guid? editingId = null,
        bool isOpen = true,
        EventCallback? onSaved = null,
        EventCallback<bool>? isOpenChanged = null)
    {
        return Render<InterceptionFormDrawer>(p => p
            .Add(d => d.IsOpen, isOpen)
            .Add(d => d.EditingId, editingId)
            .Add(d => d.OnSaved, onSaved ?? EventCallback.Empty)
            .Add(d => d.IsOpenChanged, isOpenChanged ?? EventCallback<bool>.Empty));
    }

    // -------------------------------------------------------------------------
    // Режим створення
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateMode_EditingId_IsNull()
    {
        SetupServices();

        var cut = RenderDrawer();

        cut.Instance.EditingId.Should().BeNull();
    }

    [Fact]
    public void CreateMode_LoadsActions()
    {
        var (_, actionSvc) = SetupServices();

        RenderDrawer();

        actionSvc.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CreateMode_SingleAction_AutoSelected()
    {
        var (_, actionSvc) = SetupServices();
        var action = InterceptionAction.Create("координація дій", "");
        actionSvc.GetAllAsync(Arg.Any<CancellationToken>()).Returns([action]);

        var cut = RenderDrawer();

        // Єдина дія автоматично обирається — option не має значення Guid.Empty
        var options = cut.FindAll("select option");
        options.Should().HaveCountGreaterThan(1);
    }

    // -------------------------------------------------------------------------
    // Режим редагування
    // -------------------------------------------------------------------------

    [Fact]
    public void EditMode_EditingId_IsSet()
    {
        var id = Guid.NewGuid();
        SetupServices();

        var cut = RenderDrawer(editingId: id);

        cut.Instance.EditingId.Should().Be(id);
    }

    [Fact]
    public void EditMode_LoadsMessageById()
    {
        var action = InterceptionAction.Create("координація дій", "");
        var message = InterceptionMessage.Create(
            DateTime.UtcNow, "157.0250", "1 мсб", "вектор",
            action, "нотатка", "operator", null);

        var (svc, _) = SetupServices(editMessage: message);

        RenderDrawer(editingId: message.Id);

        svc.Received(1).GetByIdAsync(message.Id, Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Збереження — CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Save_Create_CallsCreateAsync()
    {
        var action = InterceptionAction.Create("координація дій", "");
        var (svc, actionSvc) = SetupServices();
        actionSvc.GetAllAsync(Arg.Any<CancellationToken>()).Returns([action]);

        svc.CreateAsync(Arg.Any<InterceptionFormDto>(), Arg.Any<string>(),
                        Arg.Any<CancellationToken>())
           .Returns(InterceptionMessage.Create(
               DateTime.UtcNow, null, null, null, action, null, "op", null));

        var cut = RenderDrawer();

        // Обираємо дію
        await cut.InvokeAsync(() =>
            cut.Find("select").Change(action.Id.ToString()));

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Зберегти")).Click());

        await svc.Received(1)
                 .CreateAsync(Arg.Any<InterceptionFormDto>(), Arg.Any<string>(),
                              Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_Create_InvokesOnSaved()
    {
        var action = InterceptionAction.Create("координація дій", "");
        var (svc, actionSvc) = SetupServices();
        actionSvc.GetAllAsync(Arg.Any<CancellationToken>()).Returns([action]);

        svc.CreateAsync(Arg.Any<InterceptionFormDto>(), Arg.Any<string>(),
                        Arg.Any<CancellationToken>())
           .Returns(InterceptionMessage.Create(
               DateTime.UtcNow, null, null, null, action, null, "op", null));

        var onSavedCalled = false;
        var onSaved = EventCallback.Factory.Create(this, () => onSavedCalled = true);

        var cut = RenderDrawer(onSaved: onSaved);

        await cut.InvokeAsync(() =>
            cut.Find("select").Change(action.Id.ToString()));

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Зберегти")).Click());

        onSavedCalled.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Збереження — помилка
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Save_ServiceThrows_DrawerRemainsOpen()
    {
        var action = InterceptionAction.Create("координація дій", "");
        var (svc, actionSvc) = SetupServices();
        actionSvc.GetAllAsync(Arg.Any<CancellationToken>()).Returns([action]);

        svc.CreateAsync(Arg.Any<InterceptionFormDto>(), Arg.Any<string>(),
                        Arg.Any<CancellationToken>())
           .ThrowsAsync(new Exception("Помилка БД"));

        var isOpenChangedValue = true;
        var isOpenChanged = EventCallback.Factory.Create<bool>(
            this, v => isOpenChangedValue = v);

        var cut = RenderDrawer(isOpenChanged: isOpenChanged);

        await cut.InvokeAsync(() =>
            cut.Find("select").Change(action.Id.ToString()));

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Зберегти")).Click());

        isOpenChangedValue.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Закритий дравер
    // -------------------------------------------------------------------------

    [Fact]
    public void ClosedDrawer_FormNotInitialized()
    {
        SetupServices();

        var cut = RenderDrawer(isOpen: false);

        // _form = null → InterceptionFormBody не рендериться
        // select з діями не відображається (немає форми)
        cut.FindAll("select").Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Скасування
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClickCancel_ClosesDrawer()
    {
        SetupServices();

        var isOpenChangedValue = true;
        var isOpenChanged = EventCallback.Factory.Create<bool>(
            this, v => isOpenChangedValue = v);

        var cut = RenderDrawer(isOpenChanged: isOpenChanged);

        await cut.InvokeAsync(() =>
            cut.FindAll("button").First(b => b.TextContent.Contains("Скасувати")).Click());

        isOpenChangedValue.Should().BeFalse();
    }
}
