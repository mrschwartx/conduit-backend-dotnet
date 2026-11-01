using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Domain;
using Taskly.UnitTests.Support;

namespace Taskly.UnitTests.Users;

public sealed class UserDomainTests
{
    private static User BuildUser() => User.Register(
        Ids.User,
        Username.Create("johndoe123"),
        Email.Create("johndoe123@mail.com"),
        "hashed:password123",
        Ids.FixedNow);

    [Fact]
    public void RegistersWithTheDefaultAvatarAndEqualTimestamps()
    {
        var user = BuildUser();

        Assert.Equal(Ids.User, user.Id);
        Assert.Equal(AvatarPaths.DefaultPath, user.Avatar);
        Assert.Equal(Ids.FixedNow, user.CreatedAt);
        Assert.Equal(user.CreatedAt, user.UpdatedAt);
    }

    [Fact]
    public void UpdatesTheProfilePartiallyAndTouchesUpdatedAt()
    {
        var user = BuildUser();
        var later = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero);

        user.UpdateProfile(Username.Create("renamed"), null, "new-hash", later);

        Assert.Equal("renamed", user.Username.Value);
        Assert.Equal("johndoe123@mail.com", user.Email.Value);
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(later, user.UpdatedAt);
        Assert.Equal(Ids.FixedNow, user.CreatedAt);
    }

    [Fact]
    public void ChangesTheAvatar()
    {
        var user = BuildUser();
        var later = new DateTimeOffset(2026, 4, 4, 0, 0, 0, TimeSpan.Zero);

        user.ChangeAvatar("/images/users/avatar-1-2.png", later);

        Assert.Equal("/images/users/avatar-1-2.png", user.Avatar);
        Assert.Equal(later, user.UpdatedAt);
    }

    [Fact]
    public void RestoresStoredValuesUntouched()
    {
        var created = new DateTimeOffset(2025, 5, 5, 0, 0, 0, TimeSpan.Zero);
        var updated = created.AddDays(1);

        var user = User.Restore(Ids.User, Username.Restore("abc"), Email.Restore("a@b.co"), "hash", "/images/users/default-avatar.jpg", created, updated);

        Assert.Equal(created, user.CreatedAt);
        Assert.Equal(updated, user.UpdatedAt);
    }

    [Fact]
    public void RecognisesCustomAvatarsOnly()
    {
        Assert.True(AvatarPaths.IsCustom("/images/users/avatar-1700000000000-42.png"));
        Assert.False(AvatarPaths.IsCustom(AvatarPaths.DefaultPath));
        Assert.False(AvatarPaths.IsCustom("/images/users/avatar-../../etc/passwd"));
        Assert.False(AvatarPaths.IsCustom("/images/users/avatar-a/b"));
        Assert.False(AvatarPaths.IsCustom(string.Empty));
        Assert.False(AvatarPaths.IsCustom(null));
        Assert.Equal("/images/users/avatar-1.png", AvatarPaths.ToPath("avatar-1.png"));
    }

    [Fact]
    public void MapsContentTypesToImageTypes()
    {
        Assert.Same(AvatarImageType.Png, AvatarImageType.FromContentType("image/PNG"));
        Assert.Equal(".jpg", AvatarImageType.FromContentType("image/jpeg")!.Extension);
        Assert.Equal(".webp", AvatarImageType.FromContentType("image/webp")!.Extension);
        Assert.Equal(".gif", AvatarImageType.FromContentType("image/gif")!.Extension);
        Assert.Null(AvatarImageType.FromContentType("text/html"));
        Assert.Null(AvatarImageType.FromContentType(null));
    }

    public sealed class EmailTests
    {
        [Fact]
        public void TrimsAndKeepsTheCasing()
        {
            Assert.Equal("John@Mail.com", Email.Create("  John@Mail.com ").Value);
        }

        [Fact]
        public void RejectsInvalidValues()
        {
            foreach (var value in new object?[] { string.Empty, "plain", "a@b", "@mail.com", "a b@mail.com", 42, null })
            {
                Assert.Throws<ValidationException>(() => Email.Create(value));
            }
        }

        [Fact]
        public void RejectsOverlyLongAddresses()
        {
            var address = new string('a', 250) + "@b.co";

            Assert.Throws<ValidationException>(() => Email.Create(address));
        }
    }

    public sealed class UsernameTests
    {
        [Fact]
        public void TrimsAndEnforcesBounds()
        {
            Assert.Equal("abc", Username.Create("  abc ").Value);
            Assert.Equal(new string('a', 50), Username.Create(new string('a', 50)).Value);
            foreach (var value in new object?[] { "ab", new string('a', 51), "   ", 7, null })
            {
                Assert.Throws<ValidationException>(() => Username.Create(value));
            }
        }
    }

    public sealed class PlainPasswordTests
    {
        [Fact]
        public void AcceptsPasswordsWithinBounds()
        {
            Assert.Equal("password", PlainPassword.Create("password").Value);
            Assert.Equal(72, PlainPassword.Create(new string('a', 72)).Value.Length);
        }

        [Fact]
        public void RejectsShortLongAndNonStringPasswords()
        {
            var tooShort = Assert.Throws<ValidationException>(() => PlainPassword.Create("short"));
            var tooLong = Assert.Throws<ValidationException>(() => PlainPassword.Create(new string('a', 73)));
            var multibyte = Assert.Throws<ValidationException>(() => PlainPassword.Create(new string('\u20AC', 25)));
            var missing = Assert.Throws<ValidationException>(() => PlainPassword.Create(null));

            Assert.Equal("Password must be at least 8 characters long", tooShort.Message);
            Assert.Equal("Password must not exceed 72 bytes", tooLong.Message);
            Assert.Equal("Password must not exceed 72 bytes", multibyte.Message);
            Assert.Equal("Password is required", missing.Message);
        }

        [Fact]
        public void NeverPrintsTheSecret()
        {
            Assert.DoesNotContain("password", PlainPassword.Create("password").ToString());
        }
    }
}
