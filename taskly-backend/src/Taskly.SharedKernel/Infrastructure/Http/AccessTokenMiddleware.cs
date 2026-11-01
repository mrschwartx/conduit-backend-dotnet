using Microsoft.AspNetCore.Http;

namespace Taskly.SharedKernel.Infrastructure.Http;

public sealed class AccessTokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IRequestAuthenticator authenticator)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<RequireAccessTokenMetadata>() is not null)
        {
            var user = await authenticator.AuthenticateAsync(context, context.RequestAborted);
            context.SetAuthenticatedUser(user);
        }

        await next(context);
    }
}
