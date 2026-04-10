//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Interception.UI.Application.Registry.Abstractions;
using Interception.UI.Application.Registry.Dtos;
using Interception.UI.Application.Toasts;
using Interception.UI.Components.Pages.Registry.InterceptionActions;
using Interception.UI.Components.Pages.Registry.InterceptionActions.Drawers;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Interception.Tests.Components.Registry.InterceptionActions;

public sealed class ActionCatalogTests : BunitContext
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private IInterceptionActionService SetupService(
        IReadOnlyList<InterceptionActionListItemDto>? actions = null)
    {
        var svc = Substitute.For<IInterceptionActionService>();

        svc.GetAllAsync(Arg.Any<CancellationToken>())
           .Returns(actions ?? []);

        Services.AddSingleton(svc);
        Services.AddSingleton<ToastService>();
        ComponentFactories.AddStub<ActionFormDrawer>();

        return svc;
    }

    // -------------------------------------------------------------------------
    // Початковий стан — порожній список
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_EmptyList_ShowsEmptyState()
    {
        SetupService([]);

        var cut = Render<ActionCatalog>();

        cut.FindAll("table").Should().BeEmpty();
        cut.Markup.Should().Contain("Дій ще немає");
    }

    [Fact]
    public void Render_EmptyList_DoesNotShowTotal()
    {
        SetupService([]);

        var cut = Render<ActionCatalog>();

        // Всього показується лише коли _actions не null
        cut.Markup.Should().Contain("Всього:");
        cut.Markup.Should().Contain("0");
    }

    // -------------------------------------------------------------------------
    // Список з даними
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_WithActions_ShowsTable()
    {
        SetupService(
        [
            new InterceptionActionListItemDto { Id = Guid.NewGuid(), Name = "координація дій", Description = "опис 1" },
            new InterceptionActionListItemDto { Id = Guid.NewGuid(), Name = "доповідь 200", Description = "опис 2" },
        ]);

        var cut = Render<ActionCatalog>();

        cut.FindAll("tbody tr").Should().HaveCount(2);
        cut.Markup.Should().Contain("координація дій");
        cut.Markup.Should().Contain("доповідь 200");
    }

    [Fact]
    public void Render_ActionWithEmptyDescription_ShowsDash()
    {
        SetupService([new InterceptionActionListItemDto { Id = Guid.NewGuid(), Name = "інше", Description = "" }]);

        var cut = Render<ActionCatalog>();

        cut.Markup.Should().Contain("—");
    }

    [Fact]
    public void Render_ShowsCorrectTotalCount()
    {
        SetupService(
        [
            new InterceptionActionListItemDto { Id = Guid.NewGuid(), Name = "дія А", Description = "" },
            new InterceptionActionListItemDto { Id = Guid.NewGuid(), Name = "дія Б", Description = "" },
            new InterceptionActionListItemDto { Id = Guid.NewGuid(), Name = "дія В", Description = "" },
        ]);

        var cut = Render<ActionCatalog>();

        cut.Markup.Should().Contain("Всього:");
        cut.Markup.Should().Contain("3");
    }

    // -------------------------------------------------------------------------
    // Відкриття дравера
    // -------------------------------------------------------------------------

    [Fact]
    public void ClickAdd_OpensDrawerInCreateMode()
    {
        SetupService([]);

        var cut = Render<ActionCatalog>();

        cut.Find("button.btn-success").Click();

        var drawer = cut.FindComponent<Stub<ActionFormDrawer>>();
        drawer.Instance.Parameters.Get(d => d.IsOpen).Should().BeTrue();
        drawer.Instance.Parameters.Get(d => d.EditingAction).Should().BeNull();
    }

    [Fact]
    public void ClickEdit_OpensDrawerWithAction()
    {
        var action = new InterceptionActionListItemDto { Id = Guid.NewGuid(), Name = "координація дій", Description = "" };
        SetupService([action]);

        var cut = Render<ActionCatalog>();

        cut.Find("button.btn-secondary").Click();

        var drawer = cut.FindComponent<Stub<ActionFormDrawer>>();
        drawer.Instance.Parameters.Get(d => d.IsOpen).Should().BeTrue();
        drawer.Instance.Parameters.Get(d => d.EditingAction).Should().Be(action);
    }

    [Fact]
    public void ClickAddFirstAction_EmptyState_OpensDrawer()
    {
        SetupService([]);

        var cut = Render<ActionCatalog>();

        cut.Find("button.btn-success").Click();

        var drawer = cut.FindComponent<Stub<ActionFormDrawer>>();
        drawer.Instance.Parameters.Get(d => d.IsOpen).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Перезавантаження після збереження
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OnSaved_ReloadsActions()
    {
        var svc = SetupService([]);
        var cut = Render<ActionCatalog>();

        // Після першого рендеру — список порожній
        cut.Markup.Should().Contain("Дій ще немає");

        // Сервіс тепер повертає нову дію
        svc.GetAllAsync(Arg.Any<CancellationToken>())
           .Returns([new InterceptionActionListItemDto { Id = Guid.NewGuid(), Name = "нова дія", Description = "" }]);

        // Симулюємо OnSaved з дравера — він викликає LoadAsync через EventCallback
        var drawer = cut.FindComponent<Stub<ActionFormDrawer>>();
        await cut.InvokeAsync(() =>
            drawer.Instance.Parameters.Get(d => d.OnSaved).InvokeAsync());

        cut.Markup.Should().Contain("нова дія");
    }

    // -------------------------------------------------------------------------
    // Помилка завантаження
    // -------------------------------------------------------------------------

    [Fact]
    public void LoadAsync_ServiceThrows_DoesNotCrash()
    {
        var svc = Substitute.For<IInterceptionActionService>();
        svc.GetAllAsync(Arg.Any<CancellationToken>())
           .ThrowsAsync(new Exception("DB error"));

        Services.AddSingleton(svc);
        Services.AddSingleton<ToastService>();
        ComponentFactories.AddStub<ActionFormDrawer>();

        var act = () => Render<ActionCatalog>();

        act.Should().NotThrow();
    }
}
