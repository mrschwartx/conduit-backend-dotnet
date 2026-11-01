using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Taskly.Modules.Tasks.Application;
using Taskly.Modules.Tasks.Domain;
using Taskly.Modules.Tasks.Infrastructure;
using Taskly.Modules.Tasks.Presentation;
using Taskly.SharedKernel.Application;
using Taskly.SharedKernel.Domain;
using Taskly.SharedKernel.Infrastructure.Persistence;

namespace Taskly.Modules.Tasks;

public static class TasksModule
{
    public static IServiceCollection AddTasksModule(this IServiceCollection services)
    {
        services.AddSingleton<ITaskRepository, PostgresTaskRepository>();

        services.AddSingleton<CreateTask>();
        services.AddSingleton<GetTask>();
        services.AddSingleton<ListTasksByUser>();
        services.AddSingleton<UpdateTask>();
        services.AddSingleton<DeleteTask>();
        services.AddSingleton<DeleteTasksByOwner>();

        services.AddSingleton<IIntegrationEventHandler<UserDeleted>, UserDeletedHandler>();
        services.AddSingleton<IMigrationSource>(new EmbeddedMigrationSource(typeof(TasksModule).Assembly));

        return services;
    }

    public static IEndpointRouteBuilder MapTasksEndpoints(this IEndpointRouteBuilder routes) =>
        routes.MapTasksManagementEndpoints();
}
