//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using FluentAssertions;
using Interception.Application.Toasts;

namespace Interception.Tests.Application.Toasts;

public sealed class ToastServiceTests
{
    [Fact]
    public void Info_RaisesOnShowEvent_WithInfoKind()
    {
        var service = new ToastService();
        ToastMessage? raised = null;
        service.OnShow += msg => raised = msg;

        service.Info("Заголовок", "Тіло");

        raised.Should().NotBeNull();
        raised!.Kind.Should().Be(ToastKind.Info);
        raised.Title.Should().Be("Заголовок");
        raised.Body.Should().Be("Тіло");
        raised.AutoHideMs.Should().Be(4000);
    }

    [Fact]
    public void Error_RaisesOnShowEvent_WithExtendedAutoHide()
    {
        var service = new ToastService();
        ToastMessage? raised = null;
        service.OnShow += msg => raised = msg;

        service.Error("Помилка", "Деталі");

        raised.Should().NotBeNull();
        raised!.Kind.Should().Be(ToastKind.Error);
        raised.AutoHideMs.Should().Be(7000);
    }
}
