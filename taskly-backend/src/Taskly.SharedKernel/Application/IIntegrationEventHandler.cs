using Taskly.SharedKernel.Domain;

namespace Taskly.SharedKernel.Application;

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
