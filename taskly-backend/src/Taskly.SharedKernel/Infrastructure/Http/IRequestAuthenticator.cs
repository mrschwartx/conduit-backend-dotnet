using Microsoft.AspNetCore.Http;
using Taskly.SharedKernel.Application;

namespace Taskly.SharedKernel.Infrastructure.Http;

public interface IRequestAuthenticator
{
    Task<AuthenticatedUser> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken);
}
