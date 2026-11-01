using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Taskly.Modules.Users.Application;
using Taskly.SharedKernel.Infrastructure.Http;

namespace Taskly.Modules.Users.Presentation;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersManagementEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/users").RequireAccessToken();

        group.MapGet("/", async (HttpContext context, ListUsers listUsers, CancellationToken cancellationToken) =>
        {
            var users = await listUsers.ExecuteAsync(
                QueryParameters.Value(context.Request.Query, "username"),
                QueryParameters.Value(context.Request.Query, "email"),
                cancellationToken);
            return Results.Ok(users);
        });

        group.MapGet("/{id}", async (string id, GetUser getUser, CancellationToken cancellationToken) =>
            Results.Ok(await getUser.ExecuteAsync(id, cancellationToken)));

        group.MapPatch("/update/{id}", async (
            string id,
            HttpContext context,
            UpdateUser updateUser,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            var body = await RequestBodyReader.ReadJsonAsync(context.Request, cancellationToken);
            return Results.Ok(await updateUser.ExecuteAsync(requester.UserId, id, body, cancellationToken));
        });

        group.MapPost("/upload/{id}", async (
            string id,
            HttpContext context,
            ChangeUserAvatar changeUserAvatar,
            UsersModuleOptions options,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            var upload = await AvatarUploadReader.ReadAsync(context.Request, options.AvatarMaxBytes, cancellationToken);
            return Results.Ok(await changeUserAvatar.ExecuteAsync(requester.UserId, id, upload, cancellationToken));
        });

        group.MapDelete("/delete/{id}", async (
            string id,
            HttpContext context,
            DeleteUser deleteUser,
            AuthCookie cookie,
            CancellationToken cancellationToken) =>
        {
            var requester = context.GetAuthenticatedUser();
            await deleteUser.ExecuteAsync(requester.UserId, id, cancellationToken);
            cookie.Clear(context.Response);
            return Results.Ok(new MessageResponse("User has been deleted"));
        });

        return routes;
    }
}
