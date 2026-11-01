using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Application;

public sealed class RegisterUser(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    TimeProvider clock)
{
    public async Task<AuthResult> ExecuteAsync(
        IReadOnlyDictionary<string, object?> input,
        CancellationToken cancellationToken = default)
    {
        var username = Username.Create(input.GetValueOrDefault("username"));
        var email = Email.Create(input.GetValueOrDefault("email"));
        var password = PlainPassword.Create(input.GetValueOrDefault("password"));

        if (await users.ExistsByEmailOrUsernameAsync(email.Value, username.Value, null, cancellationToken))
        {
            throw new AlreadyExistsException("Email or Username is already registered.");
        }

        var passwordHash = passwordHasher.Hash(password.Value);
        var user = User.Register(users.NextId(), username, email, passwordHash, Timestamps.Now(clock));
        await users.AddAsync(user, cancellationToken);

        return new AuthResult(UserDto.From(user), tokens.Issue(user.Id));
    }
}
