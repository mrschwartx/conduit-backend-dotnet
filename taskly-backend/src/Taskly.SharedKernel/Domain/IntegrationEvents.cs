namespace Taskly.SharedKernel.Domain;

public interface IIntegrationEvent
{
    DateTimeOffset OccurredAt { get; }
}

public sealed record UserDeleted(Guid UserId, DateTimeOffset OccurredAt) : IIntegrationEvent;
