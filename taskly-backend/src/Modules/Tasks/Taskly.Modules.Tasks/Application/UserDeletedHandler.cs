using Taskly.SharedKernel.Application;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Tasks.Application;

public sealed class UserDeletedHandler(DeleteTasksByOwner deleteTasksByOwner) : IIntegrationEventHandler<UserDeleted>
{
    public Task HandleAsync(UserDeleted integrationEvent, CancellationToken cancellationToken) =>
        deleteTasksByOwner.ExecuteAsync(integrationEvent.UserId, cancellationToken);
}
