using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Taskly.SharedKernel.Application;
using Taskly.SharedKernel.Domain;
using Taskly.SharedKernel.Infrastructure.Events;

namespace Taskly.UnitTests.SharedKernel;

public sealed class InMemoryEventBusTests
{
    private static readonly UserDeleted Event = new(Guid.NewGuid(), DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task DeliversEventsToEverySubscribedHandler()
    {
        var first = new CountingHandler();
        var second = new CountingHandler();
        var bus = CreateBus(first, second);

        await bus.PublishAsync(Event);

        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
        Assert.Equal(Event, first.Last);
    }

    [Fact]
    public async Task ContinuesWhenAHandlerFails()
    {
        var after = new CountingHandler();
        var bus = CreateBus(new FailingHandler(), after);

        await bus.PublishAsync(Event);

        Assert.Equal(1, after.Calls);
    }

    [Fact]
    public async Task IgnoresEventsWithoutHandlers()
    {
        var bus = CreateBus();

        await bus.PublishAsync(Event);
    }

    private static InMemoryEventBus CreateBus(params IIntegrationEventHandler<UserDeleted>[] handlers)
    {
        var services = new ServiceCollection();
        foreach (var handler in handlers)
        {
            services.AddSingleton(handler);
        }

        var provider = services.BuildServiceProvider();
        return new InMemoryEventBus(provider, NullLogger<InMemoryEventBus>.Instance);
    }

    private sealed class CountingHandler : IIntegrationEventHandler<UserDeleted>
    {
        public int Calls { get; private set; }

        public UserDeleted? Last { get; private set; }

        public Task HandleAsync(UserDeleted integrationEvent, CancellationToken cancellationToken)
        {
            Calls++;
            Last = integrationEvent;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingHandler : IIntegrationEventHandler<UserDeleted>
    {
        public Task HandleAsync(UserDeleted integrationEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }
}
