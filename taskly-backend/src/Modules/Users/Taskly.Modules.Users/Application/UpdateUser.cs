using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Application;

public sealed class UpdateUser(IUserRepository users, IPasswordHasher passwordHasher, TimeProvider clock)
{
    public async Task<UserDto> ExecuteAsync(
        Guid requesterId,
        string userId,
        IReadOnlyDictionary<string, object?> changes,
        CancellationToken cancellationToken = default)
    {
        OwnAccount.AssertOwned(requesterId, userId, "You can only update your own account");
        var id = EntityIds.Parse(userId, "user");

        var hasUsername = changes.ContainsKey("username");
        var hasEmail = changes.ContainsKey("email");
        var hasPassword = changes.ContainsKey("password");
        if (!hasUsername && !hasEmail && !hasPassword)
        {
            throw new ValidationException("No updatable fields provided");
        }

        var username = hasUsername ? Username.Create(changes["username"]) : null;
        var email = hasEmail ? Email.Create(changes["email"]) : null;
        var password = hasPassword ? PlainPassword.Create(changes["password"]) : null;

        var user = await users.FindByIdAsync(id, cancellationToken)
                   ?? throw new NotFoundException("User not found!");

        if (username is not null || email is not null)
        {
            var taken = await users.ExistsByEmailOrUsernameAsync(email?.Value, username?.Value, id, cancellationToken);
            if (taken)
            {
                throw new AlreadyExistsException("Email or Username is already registered.");
            }
        }

        var passwordHash = password is not null ? passwordHasher.Hash(password.Value) : null;
        user.UpdateProfile(username, email, passwordHash, Timestamps.Now(clock));

        if (!await users.UpdateAsync(user, cancellationToken))
        {
            throw new NotFoundException("User not found!");
        }

        return UserDto.From(user);
    }
}
