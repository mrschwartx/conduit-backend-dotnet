using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Taskly.SharedKernel.Application;
using Taskly.SharedKernel.Infrastructure.Events;
using Taskly.SharedKernel.Infrastructure.Persistence;

namespace Taskly.SharedKernel;

public static class SharedKernelServiceCollectionExtensions
{
    public static IServiceCollection AddSharedKernel(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddSingleton<SqlMigrator>();
        services.AddHostedService<MigrationHostedService>();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        return services;
    }
}
