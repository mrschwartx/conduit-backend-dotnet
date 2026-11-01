using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Application;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Application;

public sealed class VerifyAccessToken(ITokenService tokens)
{
    public async Task<AuthenticatedUser> ExecuteAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new AuthenticationFailedException("Unauthorized: Token not provided");
        }

        try
        {
            return new AuthenticatedUser(await tokens.VerifyAsync(token, cancellationToken));
        }
        catch (InvalidTokenException)
        {
            throw new ForbiddenException("Forbidden: Invalid token");
        }
    }
}
