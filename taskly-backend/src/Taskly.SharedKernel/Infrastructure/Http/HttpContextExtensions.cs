using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Taskly.SharedKernel.Application;
using Taskly.SharedKernel.Domain;

namespace Taskly.SharedKernel.Infrastructure.Http;

public static class HttpContextExtensions
{
    private const string AuthenticatedUserKey = "Taskly.AuthenticatedUser";

    public static void SetAuthenticatedUser(this HttpContext context, AuthenticatedUser user) =>
        context.Items[AuthenticatedUserKey] = user;

    public static AuthenticatedUser GetAuthenticatedUser(this HttpContext context)
    {
        if (context.Items.TryGetValue(AuthenticatedUserKey, out var value) && value is AuthenticatedUser user)
        {
            return user;
        }

        throw new AuthenticationFailedException("Unauthorized: Token not provided");
    }

    public static TBuilder RequireAccessToken<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(RequireAccessTokenMetadata.Instance);
        builder.AddEndpointFilter(async (invocation, next) =>
        {
            invocation.HttpContext.GetAuthenticatedUser();
            return await next(invocation);
        });
        return builder;
    }
}
