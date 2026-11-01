using Microsoft.AspNetCore.Http;
using Taskly.Modules.Users.Application;
using Taskly.SharedKernel.Application;
using Taskly.SharedKernel.Infrastructure.Http;

namespace Taskly.Modules.Users.Presentation;

public sealed class CookieRequestAuthenticator(VerifyAccessToken verifyAccessToken, AuthCookie cookie)
    : IRequestAuthenticator
{
    public Task<AuthenticatedUser> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        context.Request.Cookies.TryGetValue(cookie.Name, out var token);
        return verifyAccessToken.ExecuteAsync(token, cancellationToken);
    }
}
