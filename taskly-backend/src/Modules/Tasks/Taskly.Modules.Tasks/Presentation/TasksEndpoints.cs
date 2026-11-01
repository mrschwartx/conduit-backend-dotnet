using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Taskly.Modules.Tasks.Application;
using Taskly.SharedKernel.Infrastructure.Http;

namespace Taskly.Modules.Tasks.Presentation;

public static class TasksEndpoints
{
    public static IEndpointRouteBuilder MapTasksManagementEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/tasks").RequireAccessToken();

        group.MapGet("/user/{id}", async (
            string id,
            HttpContext context,
            ListTasksByUser listTasksByUser,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            var result = await listTasksByUser.ExecuteAsync(
                requester.UserId,
                id,
                QueryParameters.Value(context.Request.Query, "status"),
                QueryParameters.Value(context.Request.Query, "orderBy"),
                QueryParameters.Value(context.Request.Query, "page"),
                cancellationToken);
            return Results.Ok(result);
        });

        group.MapGet("/{id}", async (
            string id,
            HttpContext context,
            GetTask getTask,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            return Results.Ok(await getTask.ExecuteAsync(requester.UserId, id, cancellationToken));
        });

        group.MapPost("/create", async (
            HttpContext context,
            CreateTask createTask,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            var body = await RequestBodyReader.ReadJsonAsync(context.Request, cancellationToken);
            return Results.Ok(await createTask.ExecuteAsync(requester.UserId, body, cancellationToken));
        });

        group.MapPatch("/{id}", async (
            string id,
            HttpContext context,
            UpdateTask updateTask,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            var body = await RequestBodyReader.ReadJsonAsync(context.Request, cancellationToken);
            return Results.Ok(await updateTask.ExecuteAsync(requester.UserId, id, body, cancellationToken));
        });

        group.MapDelete("/{id}", async (
            string id,
            HttpContext context,
            DeleteTask deleteTask,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            await deleteTask.ExecuteAsync(requester.UserId, id, cancellationToken);
            return Results.Ok(new MessageResponse("Task has been deleted"));
        });

        return routes;
    }
}
