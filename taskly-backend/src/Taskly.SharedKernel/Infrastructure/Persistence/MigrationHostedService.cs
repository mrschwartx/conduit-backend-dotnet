using Microsoft.Extensions.Hosting;

namespace Taskly.SharedKernel.Infrastructure.Persistence;

public sealed class MigrationHostedService(SqlMigrator migrator) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken) =>
        await migrator.MigrateAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
