using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Application;

public sealed class AuthenticateUser(IUserRepository users, IPasswordHasher passwordHasher, ITokenService tokens)
{
    public async Task<AuthResult> ExecuteAsync(
        IReadOnlyDictionary<string, object?> input,
        CancellationToken cancellationToken = default)
    {
        if (input.GetValueOrDefault("email") is not string email
            || input.GetValueOrDefault("password") is not string password)
        {
            throw new ValidationException("Email and password are required");
        }

        var user = await users.FindByEmailAsync(TextRules.Trim(email), cancellationToken)
                   ?? throw new NotFoundException("User not found!");

        if (!passwordHasher.Matches(password, user.PasswordHash))
        {
            throw new AuthenticationFailedException("Wrong password!");
        }

        return new AuthResult(UserDto.From(user), tokens.Issue(user.Id));
    }
}
