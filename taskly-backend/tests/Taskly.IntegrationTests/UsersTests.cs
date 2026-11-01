using Taskly.IntegrationTests.Support;

namespace Taskly.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class UsersTests(ApiFixture api)
{
    private static readonly byte[] PngBytes = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3];

    [Fact]
    public async Task ListsUsersAndFiltersByUsernameAndEmail()
    {
        var user = await api.RegisterAsync("lister");

        var all = await user.Client.GetAsync("/api/v1/users");
        var byUsername = await user.Client.GetAsync($"/api/v1/users?username={user.Username.ToUpperInvariant()}");
        var byEmail = await user.Client.GetAsync($"/api/v1/users?email={user.Email}");

        Assert.Equal(200, all.Status);
        Assert.True(all.Json.GetArrayLength() >= 1);
        Assert.Equal(1, byUsername.Json.GetArrayLength());
        Assert.Equal(user.Id, byUsername.Json[0].GetProperty("_id").GetString());
        Assert.Equal(1, byEmail.Json.GetArrayLength());
    }

    [Fact]
    public async Task TreatsFilterCharactersLiterally()
    {
        var user = await api.RegisterAsync();

        var regex = await user.Client.GetAsync("/api/v1/users?username=.*");
        var wildcard = await user.Client.GetAsync("/api/v1/users?username=%25");
        var underscore = await user.Client.GetAsync("/api/v1/users?username=%5F%5F%5F%5F%5F%5F%5F%5F%5F%5F%5F%5F%5F%5F%5F%5F%5F");

        Assert.Equal(404, regex.Status);
        Assert.Equal("No users found", regex.Text("message"));
        Assert.Equal(404, wildcard.Status);
        Assert.Equal(404, underscore.Status);
    }

    [Fact]
    public async Task RejectsRepeatedFilters()
    {
        var user = await api.RegisterAsync();

        var response = await user.Client.GetAsync("/api/v1/users?username=a&username=b");

        Assert.Equal(400, response.Status);
        Assert.Equal("Username filter must be a string", response.Text("message"));
    }

    [Fact]
    public async Task GetsUsersByIdAndReportsBadIds()
    {
        var user = await api.RegisterAsync();

        var found = await user.Client.GetAsync("/api/v1/users/" + user.Id);
        var malformed = await user.Client.GetAsync("/api/v1/users/not-an-id");
        var unknown = await user.Client.GetAsync("/api/v1/users/" + Guid.NewGuid());

        Assert.Equal(200, found.Status);
        Assert.Equal(user.Email, found.Text("email"));
        Assert.Equal(400, malformed.Status);
        Assert.Equal("Invalid user ID format", malformed.Text("message"));
        Assert.Equal(404, unknown.Status);
    }

    [Fact]
    public async Task UpdatesTheOwnAccountAndIgnoresUnknownFields()
    {
        var user = await api.RegisterAsync();
        var newName = "renamed_" + Guid.NewGuid().ToString("N")[..8];

        var response = await user.Client.PatchJsonAsync(
            "/api/v1/users/update/" + user.Id,
            ApiClient.Json(("username", newName), ("avatar", "hacked.jpg"), ("password", "BrandNewPassword1")));

        Assert.Equal(200, response.Status);
        Assert.Equal(newName, response.Text("username"));
        Assert.Equal("/images/users/default-avatar.jpg", response.Text("avatar"));

        var signIn = await api.CreateClient().PostJsonAsync(
            "/api/v1/auth/signin",
            ApiClient.Json(("email", user.Email), ("password", "BrandNewPassword1")));
        Assert.Equal(200, signIn.Status);
    }

    [Fact]
    public async Task UpdateRejectsForeignAccountsCollisionsAndEmptyBodies()
    {
        var first = await api.RegisterAsync();
        var second = await api.RegisterAsync();

        var foreign = await second.Client.PatchJsonAsync("/api/v1/users/update/" + first.Id, ApiClient.Json(("username", "hijacked_name")));
        var collision = await second.Client.PatchJsonAsync("/api/v1/users/update/" + second.Id, ApiClient.Json(("username", first.Username)));
        var empty = await second.Client.PatchJsonAsync("/api/v1/users/update/" + second.Id, "{}");

        Assert.Equal(401, foreign.Status);
        Assert.Equal("You can only update your own account", foreign.Text("message"));
        Assert.Equal(422, collision.Status);
        Assert.Equal(400, empty.Status);
        Assert.Equal("No updatable fields provided", empty.Text("message"));
    }

    [Fact]
    public async Task UploadsAnAvatarServesItAndReplacesTheOldOne()
    {
        var user = await api.RegisterAsync();

        var first = await user.Client.UploadAsync("/api/v1/users/upload/" + user.Id, "avatar", "me.png", "image/png", PngBytes);
        var firstPath = first.Text("avatar");
        var served = await api.CreateClient().GetAsync(firstPath);

        Assert.Equal(200, first.Status);
        Assert.Matches(@"^/images/users/avatar-[\w-]+\.png$", firstPath);
        Assert.Equal(200, served.Status);
        Assert.Equal("image/png", served.Header("Content-Type"));

        var second = await user.Client.UploadAsync("/api/v1/users/upload/" + user.Id, "avatar", "me.jpg", "image/jpeg", PngBytes);
        var secondPath = second.Text("avatar");

        Assert.Equal(200, second.Status);
        Assert.NotEqual(firstPath, secondPath);
        Assert.EndsWith(".jpg", secondPath);
        Assert.Equal(404, (await api.CreateClient().GetAsync(firstPath)).Status);
        Assert.Equal(200, (await api.CreateClient().GetAsync(secondPath)).Status);
    }

    [Fact]
    public async Task AvatarUploadRejectsInvalidRequests()
    {
        var user = await api.RegisterAsync();
        var other = await api.RegisterAsync();
        var path = "/api/v1/users/upload/" + user.Id;
        var tooLarge = new byte[(2 * 1024 * 1024) + 1];

        var notImage = await user.Client.UploadAsync(path, "avatar", "x.html", "text/html", PngBytes);
        var large = await user.Client.UploadAsync(path, "avatar", "big.png", "image/png", tooLarge);
        var wrongField = await user.Client.UploadAsync(path, "photo", "me.png", "image/png", PngBytes);
        var emptyForm = await user.Client.UploadEmptyFormAsync(path);
        var notMultipart = await user.Client.PostJsonAsync(path, "{}");
        var foreign = await other.Client.UploadAsync(path, "avatar", "me.png", "image/png", PngBytes);

        Assert.Equal(400, notImage.Status);
        Assert.Equal(400, large.Status);
        Assert.Contains("2MB", large.Text("message"));
        Assert.Equal(400, wrongField.Status);
        Assert.Equal(400, emptyForm.Status);
        Assert.Equal("No file uploaded", emptyForm.Text("message"));
        Assert.Equal(400, notMultipart.Status);
        Assert.Equal(401, foreign.Status);
    }

    [Fact]
    public async Task DeletesTheOwnAccountItsTasksAndItsAvatar()
    {
        var user = await api.RegisterAsync();
        var upload = await user.Client.UploadAsync("/api/v1/users/upload/" + user.Id, "avatar", "me.png", "image/png", PngBytes);
        var avatarFile = Path.Combine(api.PublicDirectory, upload.Text("avatar").TrimStart('/'));
        var task = await user.Client.PostJsonAsync("/api/v1/tasks/create", ApiFixture.TaskJson());
        Assert.True(File.Exists(avatarFile));

        var response = await user.Client.DeleteAsync("/api/v1/users/delete/" + user.Id);

        Assert.Equal(200, response.Status);
        Assert.Equal("User has been deleted", response.Text("message"));
        Assert.False(user.Client.HasCookie);
        Assert.False(File.Exists(avatarFile));

        var signIn = await api.CreateClient().PostJsonAsync(
            "/api/v1/auth/signin",
            ApiClient.Json(("email", user.Email), ("password", user.Password)));
        Assert.Equal(404, signIn.Status);

        var survivor = await api.RegisterAsync();
        var lookup = await survivor.Client.GetAsync("/api/v1/tasks/" + task.Text("_id"));
        Assert.Equal(404, lookup.Status);
    }

    [Fact]
    public async Task DeleteRejectsForeignAccounts()
    {
        var first = await api.RegisterAsync();
        var second = await api.RegisterAsync();

        var response = await second.Client.DeleteAsync("/api/v1/users/delete/" + first.Id);

        Assert.Equal(401, response.Status);
        Assert.Equal("You can only delete your own account", response.Text("message"));
        Assert.Equal(200, (await first.Client.GetAsync("/api/v1/users/" + first.Id)).Status);
    }
}
