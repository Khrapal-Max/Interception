/*//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Interception.Application.Interceptions.Abstractions;
using Interception.Application.Interceptions.Dtos;
using Interception.Application.Toasts;
using Interception.Web.UI.Components.Pages.Interceptions;
using Interception.Web.UI.Components.Pages.Interceptions.Drawers;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Interception.Tests.Components.Interceptions;

public sealed class InterceptionRegistryTests : BunitContext
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static PagedResultDto<InterceptionListItemDto> EmptyPage()
        => new([], 0, 1, 50);

    private static PagedResultDto<InterceptionListItemDto> MakePage(
        params InterceptionListItemDto[] items)
        => new([.. items], items.Length, 1, 50);

    private static InterceptionListItemDto MakeItem(
        string frequency = "157.0250",
        string? actionName = "координація дій",
        string? division = "1 мсб",
        string? vectorSignal = "степове-олексіївка",
        DateTime? observedDate = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ObservedDate = observedDate ?? new DateTime(2026, 3, 14, 16, 30, 0, DateTimeKind.Utc),
            Frequency = frequency,
            Division = division,
            VectorSignal = vectorSignal,
            ActionName = actionName,
            Participants = [],
            Labels = [],
        };

    private (IInterceptionService svc, IInterceptionActionService actionSvc) SetupServices(
        PagedResultDto<InterceptionListItemDto>? page = null)
    {
        var svc = Substitute.For<IInterceptionService>();
        var actionSvc = Substitute.For<IInterceptionActionService>();

        svc.GetPagedAsync(Arg.Any<InterceptionFilterDto>(), Arg.Any<int>(),
                          Arg.Any<int>(), Arg.Any<CancellationToken>())
           .Returns(page ?? EmptyPage());

        svc.GetFrequencyWithDivisionAsync(Arg.Any<string?>(), Arg.Any<int>(),
                                          Arg.Any<CancellationToken>())
           .Returns([]);

        svc.GetVectorSignalSuggestionsAsync(Arg.Any<string?>(), Arg.Any<string?>(),
                                            Arg.Any<int>(), Arg.Any<CancellationToken>())
           .Returns([]);

        actionSvc.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        Services.AddSingleton(svc);
        Services.AddSingleton(actionSvc);
        Services.AddSingleton<ToastService>();

        // Замінюємо складні дравери заглушками
        ComponentFactories.AddStub<InterceptionFormDrawer>();
        ComponentFactories.AddStub<InterceptionFilterDrawer>();
        ComponentFactories.AddStub<InterceptionImportDrawer>();
        ComponentFactories.AddStub<InterceptionTextBlockDrawer>();

        return (svc, actionSvc);
    }

    // -------------------------------------------------------------------------
    // Початковий стан — порожній список
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_EmptyList_ShowsEmptyState()
    {
        SetupServices();

        var cut = Render<InterceptionRegistry>();

        cut.FindAll("table").Should().BeEmpty();
        cut.Markup.Should().Contain("Записів не знайдено");
    }

    [Fact]
    public void Render_EmptyList_NoFilterResetButton()
    {
        SetupServices();

        var cut = Render<InterceptionRegistry>();

        // Кнопка "Скинути фільтр" з'являється тільки при активному фільтрі
        cut.FindAll("button.btn-link").Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Таблиця з даними
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_WithItems_ShowsTable()
    {
        SetupServices(MakePage(MakeItem(), MakeItem("410.1370")));

        var cut = Render<InterceptionRegistry>();

        cut.FindAll("tbody tr").Should().HaveCount(2);
    }

    [Fact]
    public void Render_Item_ShowsFrequency()
    {
        SetupServices(MakePage(MakeItem("157.0250")));

        var cut = Render<InterceptionRegistry>();

        cut.Markup.Should().Contain("157.0250");
    }

    [Fact]
    public void Render_Item_ShowsActionName()
    {
        SetupServices(MakePage(MakeItem(actionName: "доповідь 200")));

        var cut = Render<InterceptionRegistry>();

        cut.Markup.Should().Contain("доповідь 200");
    }

    [Fact]
    public void Render_Item_NullFrequency_ShowsDash()
    {
        SetupServices(MakePage(
            new InterceptionListItemDto
            {
                Id = Guid.NewGuid(),
                ObservedDate = DateTime.UtcNow,
                Frequency = null,
                Participants = [],
                Labels = [],
            }));

        var cut = Render<InterceptionRegistry>();

        cut.Markup.Should().Contain("—");
    }

    [Fact]
    public void Render_KnownParticipant_ShowsPrimaryBadge()
    {
        var item = MakeItem();
        item.Participants.Add(new ParticipantBriefDto
        {
            Name = "ШАПКА",
            IsUnknown = false,
            Ordinal = 1
        });

        SetupServices(MakePage(item));

        var cut = Render<InterceptionRegistry>();

        cut.Find(".badge.bg-primary").TextContent.Should().Be("ШАПКА");
    }

    [Fact]
    public void Render_UnknownParticipant_ShowsSecondaryBadge()
    {
        var item = MakeItem();
        item.Participants.Add(new ParticipantBriefDto
        {
            Name = null,
            IsUnknown = true,
            Ordinal = 1
        });

        SetupServices(MakePage(item));

        var cut = Render<InterceptionRegistry>();

        cut.Find(".badge.bg-secondary").Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // Пагінація
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_SinglePage_NoPagination()
    {
        SetupServices(MakePage(MakeItem()));

        var cut = Render<InterceptionRegistry>();

        cut.FindAll("nav .pagination").Should().BeEmpty();
    }

    [Fact]
    public void Render_MultiplePages_ShowsPagination()
    {
        var (svc, _) = SetupServices();
        var bigPage = new PagedResultDto<InterceptionListItemDto>(
            [MakeItem()], totalCount: 200, page: 1, pageSize: 50);

        svc.GetPagedAsync(Arg.Any<InterceptionFilterDto>(), Arg.Any<int>(),
                          Arg.Any<int>(), Arg.Any<CancellationToken>())
           .Returns(bigPage);

        var cut = Render<InterceptionRegistry>();

        cut.FindAll(".pagination").Should().NotBeEmpty();
        // 4 сторінки: 200/50
        cut.FindAll(".page-item").Should().HaveCountGreaterThan(2);
    }

    // -------------------------------------------------------------------------
    // Відкриття драверів
    // -------------------------------------------------------------------------

    [Fact]
    public void ClickAdd_OpensFormDrawerInCreateMode()
    {
        SetupServices();

        var cut = Render<InterceptionRegistry>();

        cut.Find("button.btn-primary").Click();

        var drawer = cut.FindComponent<Stub<InterceptionFormDrawer>>();
        drawer.Instance.Parameters.Get(d => d.IsOpen).Should().BeTrue();
        drawer.Instance.Parameters.Get(d => d.EditingId).Should().BeNull();
    }

    [Fact]
    public void ClickEdit_OpensFormDrawerWithId()
    {
        var item = MakeItem();
        SetupServices(MakePage(item));

        var cut = Render<InterceptionRegistry>();

        // Кнопка редагування (btn-secondary) в рядку таблиці
        cut.Find("button.btn-secondary").Click();

        var drawer = cut.FindComponent<Stub<InterceptionFormDrawer>>();
        drawer.Instance.Parameters.Get(d => d.IsOpen).Should().BeTrue();
        drawer.Instance.Parameters.Get(d => d.EditingId).Should().Be(item.Id);
    }

    [Fact]
    public void ClickFilter_OpensFilterDrawer()
    {
        SetupServices();

        var cut = Render<InterceptionRegistry>();

        cut.Find("button.btn-outline-secondary").Click();

        var drawer = cut.FindComponent<Stub<InterceptionFilterDrawer>>();
        drawer.Instance.Parameters.Get(d => d.IsOpen).Should().BeTrue();
    }

    [Fact]
    public void ClickImport_OpensImportDrawer()
    {
        SetupServices();

        var cut = Render<InterceptionRegistry>();

        cut.FindAll("button.btn-outline-secondary")[1].Click();

        var drawer = cut.FindComponent<Stub<InterceptionImportDrawer>>();
        drawer.Instance.Parameters.Get(d => d.IsOpen).Should().BeTrue();
    }

    [Fact]
    public void ClickTextBlock_OpensTextBlockDrawer()
    {
        SetupServices();

        var cut = Render<InterceptionRegistry>();

        cut.Find("button.btn-warning").Click();

        var drawer = cut.FindComponent<Stub<InterceptionTextBlockDrawer>>();
        drawer.Instance.Parameters.Get(d => d.IsOpen).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Активний фільтр
    // -------------------------------------------------------------------------

    [Fact]
    public void ActiveFilter_ShowsBadgeOnFilterButton()
    {
        var (svc, _) = SetupServices();

        var cut = Render<InterceptionRegistry>();

        // Симулюємо застосування фільтра через OnApplied callback
        var filterDrawer = cut.FindComponent<Stub<InterceptionFilterDrawer>>();
        var onApplied = filterDrawer.Instance.Parameters
                                       .Get(d => d.OnApplied);

        cut.InvokeAsync(() => onApplied.InvokeAsync(
            new InterceptionFilterDto { Frequency = "157.0250" }));

        cut.Markup.Should().Contain("●");
    }

    [Fact]
    public void ActiveFilter_EmptyList_ShowsResetButton()
    {
        var (svc, _) = SetupServices();
        svc.GetPagedAsync(Arg.Any<InterceptionFilterDto>(), Arg.Any<int>(),
                          Arg.Any<int>(), Arg.Any<CancellationToken>())
           .Returns(EmptyPage());

        var cut = Render<InterceptionRegistry>();

        var filterDrawer = cut.FindComponent<Stub<InterceptionFilterDrawer>>();
        var onApplied = filterDrawer.Instance.Parameters.Get(d => d.OnApplied);

        cut.InvokeAsync(() => onApplied.InvokeAsync(
            new InterceptionFilterDto { Frequency = "157.0250" }));

        cut.Markup.Should().Contain("Скинути фільтр");
    }

    // -------------------------------------------------------------------------
    // Видалення
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClickDelete_CallsDeleteAsync()
    {
        var item = MakeItem();
        var (svc, _) = SetupServices(MakePage(item));

        var cut = Render<InterceptionRegistry>();

        // Таблиця завантажилась — тепер перевизначаємо для перезавантаження після видалення
        svc.GetPagedAsync(Arg.Any<InterceptionFilterDto>(), Arg.Any<int>(),
                          Arg.Any<int>(), Arg.Any<CancellationToken>())
           .Returns(EmptyPage());

        await cut.InvokeAsync(() => cut.Find("button.btn-danger").Click());

        await svc.Received(1).DeleteAsync(item.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClickDelete_AfterSuccess_ReloadsTable()
    {
        var item = MakeItem();
        var (svc, _) = SetupServices(MakePage(item));

        var cut = Render<InterceptionRegistry>();

        // Після видалення сервіс повертає порожній список
        svc.GetPagedAsync(Arg.Any<InterceptionFilterDto>(), Arg.Any<int>(),
                          Arg.Any<int>(), Arg.Any<CancellationToken>())
           .Returns(EmptyPage());

        await cut.InvokeAsync(() => cut.Find("button.btn-danger").Click());

        cut.Markup.Should().Contain("Записів не знайдено");
    }

    // -------------------------------------------------------------------------
    // Помилка завантаження
    // -------------------------------------------------------------------------

    [Fact]
    public void LoadAsync_ServiceThrows_DoesNotCrash()
    {
        var svc = Substitute.For<IInterceptionService>();
        var actionSvc = Substitute.For<IInterceptionActionService>();

        svc.GetPagedAsync(Arg.Any<InterceptionFilterDto>(), Arg.Any<int>(),
                          Arg.Any<int>(), Arg.Any<CancellationToken>())
           .ThrowsAsync(new Exception("DB error"));

        actionSvc.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        Services.AddSingleton(svc);
        Services.AddSingleton(actionSvc);
        Services.AddSingleton<ToastService>();

        ComponentFactories.AddStub<InterceptionFormDrawer>();
        ComponentFactories.AddStub<InterceptionFilterDrawer>();
        ComponentFactories.AddStub<InterceptionImportDrawer>();
        ComponentFactories.AddStub<InterceptionTextBlockDrawer>();

        var act = () => Render<InterceptionRegistry>();
        act.Should().NotThrow();
    }
}
*/