//-----------------------------------------------------------------------------
// All rights by agreement of the developer. Author data on GitHub Khrapal M.G.
//-----------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

namespace Interception.UI.Application.Common.Events;

/// <summary>
/// Проста in-process шина подій через DI-обробники.
/// </summary>
public sealed class InProcessIntegrationEventPublisher(IServiceProvider serviceProvider) : IIntegrationEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        var handlers = serviceProvider.GetServices<IIntegrationEventHandler<TEvent>>();
        foreach (var handler in handlers)
            await handler.HandleAsync(integrationEvent, ct);
    }
}
