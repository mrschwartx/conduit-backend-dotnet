using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Taskly.Modules.Users.Application;
using Taskly.SharedKernel.Infrastructure.Http;

namespace Taskly.Modules.Users.Presentation;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/auth");

        group.MapPost("/signup", async (
            HttpContext context,
            RegisterUser registerUser,
            AuthCookie cookie,
            CancellationToken cancellationToken) =>
        {
            var body = await RequestBodyReader.ReadJsonAsync(context.Request, cancellationToken);
            var result = await registerUser.ExecuteAsync(body, cancellationToken);
            cookie.Issue(context.Response, result.Token);
            return Results.Ok(result.User);
        });

        group.MapPost("/signin", async (
            HttpContext context,
            AuthenticateUser authenticateUser,
            AuthCookie cookie,
            CancellationToken cancellationToken) =>
        {
            var body = await RequestBodyReader.ReadJsonAsync(context.Request, cancellationToken);
            var result = await authenticateUser.ExecuteAsync(body, cancellationToken);
            cookie.Issue(context.Response, result.Token);
            return Results.Ok(result.User);
        });

        group.MapGet("/signout", (HttpContext context, AuthCookie cookie) =>
        {
            cookie.Clear(context.Response);
            return Results.Ok(new MessageResponse("Sign out successful"));
        });

        return routes;
    }
}
