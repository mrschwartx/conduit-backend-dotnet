using Taskly.Modules.Users.Application;
using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Domain;
using Taskly.UnitTests.Support;

namespace Taskly.UnitTests.Users;

public sealed class UserUseCaseTests
{
    private readonly UsersFixture fixture = new();

    [Fact]
    public async Task RegistersAUserAndIssuesAToken()
    {
        var result = await fixture.RegisterAsync();

        Assert.Equal("johndoe123", result.User.Username);
        Assert.Equal(AvatarPaths.DefaultPath, result.User.Avatar);
        Assert.Equal("token:" + result.User.Id, result.Token);
        Assert.Equal("2026-01-01T00:00:00.000Z", result.User.CreatedAt);
        var stored = Assert.Single(fixture.Users.Users.Values);
        Assert.Equal("hashed:password123", stored.PasswordHash);
    }

    [Fact]
    public async Task RejectsDuplicateEmailsAndUsernamesIgnoringCase()
    {
        await fixture.RegisterAsync();

        var sameEmail = await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            fixture.RegisterAsync("other_name", "JOHNDOE123@MAIL.COM"));
        var sameUsername = await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            fixture.RegisterAsync("JohnDoe123", "other@mail.com"));

        Assert.Equal("Email or Username is already registered.", sameEmail.Message);
        Assert.Equal("Email or Username is already registered.", sameUsername.Message);
        Assert.Single(fixture.Users.Users);
    }

    [Fact]
    public async Task RejectsInvalidRegistrationsWithoutStoringAnything()
    {
        var payloads = new[]
        {
            Input.Empty,
            Input.Of(("username", "ab"), ("email", "a@mail.com"), ("password", "Password123!")),
            Input.Of(("username", "valid_user"), ("email", "nope"), ("password", "Password123!")),
            Input.Of(("username", "valid_user"), ("email", "a@mail.com"), ("password", "short")),
            Input.Of(("username", 5), ("email", "a@mail.com"), ("password", "Password123!"))
        };

        foreach (var payload in payloads)
        {
            await Assert.ThrowsAsync<ValidationException>(() => fixture.Register.ExecuteAsync(payload));
        }

        Assert.Empty(fixture.Users.Users);
    }

    [Fact]
    public async Task AuthenticatesWithAnyEmailCasingAndSurroundingSpaces()
    {
        var registered = await fixture.RegisterAsync();

        var result = await fixture.Authenticate.ExecuteAsync(
            Input.Of(("email", "  JohnDoe123@Mail.com "), ("password", "password123")));

        Assert.Equal(registered.User.Id, result.User.Id);
        Assert.Equal(registered.Token, result.Token);
    }

    [Fact]
    public async Task ReportsUnknownUsersAndWrongPasswordsSeparately()
    {
        await fixture.RegisterAsync();

        var unknown = await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Authenticate.ExecuteAsync(Input.Of(("email", "ghost@mail.com"), ("password", "password123"))));
        var wrong = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            fixture.Authenticate.ExecuteAsync(Input.Of(("email", "johndoe123@mail.com"), ("password", "nope-nope"))));

        Assert.Equal("User not found!", unknown.Message);
        Assert.Equal("Wrong password!", wrong.Message);
    }

    [Fact]
    public async Task RejectsNonStringCredentials()
    {
        foreach (var payload in new[]
                 {
                     Input.Empty,
                     Input.Of(("email", new object()), ("password", "password123")),
                     Input.Of(("email", "a@mail.com"), ("password", null))
                 })
        {
            var error = await Assert.ThrowsAsync<ValidationException>(() => fixture.Authenticate.ExecuteAsync(payload));
            Assert.Equal("Email and password are required", error.Message);
        }
    }

    [Fact]
    public async Task GetsUsersByIdIgnoringCase()
    {
        var registered = await fixture.RegisterAsync();

        var found = await fixture.Get.ExecuteAsync(registered.User.Id.ToUpperInvariant());

        Assert.Equal(registered.User.Id, found.Id);
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Get.ExecuteAsync("not-an-id"));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => fixture.Get.ExecuteAsync(Ids.Missing));
        Assert.Equal("User not found!", missing.Message);
    }

    [Fact]
    public async Task ListsAndFiltersUsers()
    {
        await fixture.RegisterAsync("alice_one", "alice@mail.com");
        await fixture.RegisterAsync("bob_two", "bob@mail.com");

        var all = await fixture.List.ExecuteAsync(null, null);
        var byName = await fixture.List.ExecuteAsync("ALICE", null);
        var byEmail = await fixture.List.ExecuteAsync(null, "bob@");
        var ignoredBlank = await fixture.List.ExecuteAsync(string.Empty, string.Empty);

        Assert.Equal(2, all.Count);
        Assert.Equal("alice_one", Assert.Single(byName).Username);
        Assert.Equal("bob_two", Assert.Single(byEmail).Username);
        Assert.Equal(2, ignoredBlank.Count);
    }

    [Fact]
    public async Task ReportsEmptyListingsAndInvalidFilters()
    {
        var empty = await Assert.ThrowsAsync<NotFoundException>(() => fixture.List.ExecuteAsync(null, null));
        var invalid = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.List.ExecuteAsync(new[] { "a", "b" }, null));

        Assert.Equal("No users found", empty.Message);
        Assert.Equal("Username filter must be a string", invalid.Message);
    }

    [Fact]
    public async Task UpdatesTheUsernameAndPassword()
    {
        var registered = await fixture.RegisterAsync();
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));

        var updated = await fixture.Update.ExecuteAsync(
            Guid.Parse(registered.User.Id),
            registered.User.Id,
            Input.Of(("username", "renamed_user"), ("password", "BrandNewPassword1"), ("avatar", "hacked.jpg")));

        Assert.Equal("renamed_user", updated.Username);
        Assert.Equal(registered.User.Avatar, updated.Avatar);
        Assert.NotEqual(updated.CreatedAt, updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateRejectsForeignAccountsCollisionsAndEmptyChanges()
    {
        var first = await fixture.RegisterAsync("first_user", "first@mail.com");
        var second = await fixture.RegisterAsync("second_user", "second@mail.com");
        var firstId = Guid.Parse(first.User.Id);
        var secondId = Guid.Parse(second.User.Id);

        var foreign = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            fixture.Update.ExecuteAsync(secondId, first.User.Id, Input.Of(("username", "hijacked"))));
        var collision = await Assert.ThrowsAsync<AlreadyExistsException>(() =>
            fixture.Update.ExecuteAsync(secondId, second.User.Id, Input.Of(("username", "FIRST_USER"))));
        var empty = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Update.ExecuteAsync(firstId, first.User.Id, Input.Empty));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Update.ExecuteAsync(Guid.Parse(Ids.Missing), Ids.Missing, Input.Of(("username", "someone"))));

        Assert.Equal("You can only update your own account", foreign.Message);
        Assert.Equal("Email or Username is already registered.", collision.Message);
        Assert.Equal("No updatable fields provided", empty.Message);
        Assert.Equal("User not found!", missing.Message);
    }

    [Fact]
    public async Task KeepingYourOwnUsernameIsNotACollision()
    {
        var registered = await fixture.RegisterAsync();

        var updated = await fixture.Update.ExecuteAsync(
            Guid.Parse(registered.User.Id),
            registered.User.Id,
            Input.Of(("username", "johndoe123")));

        Assert.Equal("johndoe123", updated.Username);
    }

    [Fact]
    public async Task ChangesAndReplacesTheAvatar()
    {
        var registered = await fixture.RegisterAsync();
        var userId = Guid.Parse(registered.User.Id);

        var first = await fixture.ChangeAvatar.ExecuteAsync(userId, registered.User.Id, new AvatarUpload("image/png", [1, 2, 3]));
        var second = await fixture.ChangeAvatar.ExecuteAsync(userId, registered.User.Id, new AvatarUpload("image/jpeg", [4]));

        Assert.EndsWith(".png", first.Avatar);
        Assert.EndsWith(".jpg", second.Avatar);
        Assert.Equal(new[] { second.Avatar }, fixture.Avatars.Stored.Keys.ToArray());
        Assert.Equal(new[] { first.Avatar }, fixture.Avatars.Removed.ToArray());
    }

    [Fact]
    public async Task AvatarUploadRejectsInvalidRequestsWithoutStoringFiles()
    {
        var registered = await fixture.RegisterAsync();
        var userId = Guid.Parse(registered.User.Id);

        var none = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.ChangeAvatar.ExecuteAsync(userId, registered.User.Id, null));
        var notImage = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.ChangeAvatar.ExecuteAsync(userId, registered.User.Id, new AvatarUpload("text/html", [1])));
        var foreign = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            fixture.ChangeAvatar.ExecuteAsync(Ids.OtherUser, registered.User.Id, new AvatarUpload("image/png", [1])));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.ChangeAvatar.ExecuteAsync(Guid.Parse(Ids.Missing), Ids.Missing, new AvatarUpload("image/png", [1])));

        Assert.Equal("No file uploaded", none.Message);
        Assert.Contains("Only JPEG, PNG, WEBP and GIF", notImage.Message);
        Assert.Equal("You can only upload an avatar for your own account", foreign.Message);
        Assert.Equal("User not found!", missing.Message);
        Assert.Empty(fixture.Avatars.Stored);
    }

    [Fact]
    public async Task DeletesTheAccountRemovesTheAvatarAndPublishesAnEvent()
    {
        var registered = await fixture.RegisterAsync();
        var userId = Guid.Parse(registered.User.Id);
        var withAvatar = await fixture.ChangeAvatar.ExecuteAsync(userId, registered.User.Id, new AvatarUpload("image/png", [1]));

        await fixture.Delete.ExecuteAsync(userId, registered.User.Id);

        Assert.Empty(fixture.Users.Users);
        Assert.Contains(withAvatar.Avatar, fixture.Avatars.Removed);
        var published = Assert.IsType<UserDeleted>(Assert.Single(fixture.Events.Published));
        Assert.Equal(userId, published.UserId);
    }

    [Fact]
    public async Task DeleteRejectsForeignAndUnknownAccounts()
    {
        var registered = await fixture.RegisterAsync();

        var foreign = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            fixture.Delete.ExecuteAsync(Ids.OtherUser, registered.User.Id));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Delete.ExecuteAsync(Guid.Parse(Ids.Missing), Ids.Missing));

        Assert.Equal("You can only delete your own account", foreign.Message);
        Assert.Equal("User not found!", missing.Message);
        Assert.Single(fixture.Users.Users);
        Assert.Empty(fixture.Events.Published);
    }

    [Fact]
    public async Task VerifiesAccessTokens()
    {
        var registered = await fixture.RegisterAsync();

        var user = await fixture.Verify.ExecuteAsync(registered.Token);

        Assert.Equal(registered.User.Id, user.UserId.ToString());
    }

    [Fact]
    public async Task RejectsMissingAndInvalidAccessTokens()
    {
        foreach (var token in new string?[] { null, string.Empty })
        {
            var missing = await Assert.ThrowsAsync<AuthenticationFailedException>(() => fixture.Verify.ExecuteAsync(token));
            Assert.Equal("Unauthorized: Token not provided", missing.Message);
        }

        var invalid = await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Verify.ExecuteAsync("garbage"));
        Assert.Equal("Forbidden: Invalid token", invalid.Message);
    }
}
