using Taskly.IntegrationTests.Support;

namespace Taskly.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class TasksTests(ApiFixture api)
{
    [Fact]
    public async Task CreatesATaskOwnedByTheCaller()
    {
        var user = await api.RegisterAsync();

        var response = await user.Client.PostJsonAsync("/api/v1/tasks/create", ApiFixture.TaskJson(description: null));

        Assert.Equal(200, response.Status);
        Assert.Equal("Read Atomic Habits", response.Text("name"));
        Assert.Equal(string.Empty, response.Text("description"));
        Assert.Equal("todo", response.Text("status"));
        Assert.Equal(user.Id, response.Text("owner"));
        Assert.Equal("2026-12-08T10:00:00.000Z", response.Text("due"));
    }

    [Fact]
    public async Task IgnoresAnOwnerSuppliedInTheBody()
    {
        var user = await api.RegisterAsync();
        var other = await api.RegisterAsync();
        var body = ApiClient.Json(("name", "Task"), ("priority", "low"), ("due", "2026-12-08"), ("owner", other.Id));

        var response = await user.Client.PostJsonAsync("/api/v1/tasks/create", body);

        Assert.Equal(200, response.Status);
        Assert.Equal(user.Id, response.Text("owner"));
    }

    [Fact]
    public async Task CreateValidatesTheInput()
    {
        var user = await api.RegisterAsync();
        var payloads = new[]
        {
            "{}",
            ApiFixture.TaskJson(name: new string('a', 121)),
            ApiFixture.TaskJson(priority: "urgent"),
            ApiFixture.TaskJson(due: "someday"),
            ApiFixture.TaskJson(status: "finished")
        };

        foreach (var payload in payloads)
        {
            var response = await user.Client.PostJsonAsync("/api/v1/tasks/create", payload);
            Assert.Equal(400, response.Status);
        }
    }

    [Fact]
    public async Task GetsUpdatesAndDeletesATask()
    {
        var user = await api.RegisterAsync();
        var created = await user.Client.PostJsonAsync("/api/v1/tasks/create", ApiFixture.TaskJson());
        var id = created.Text("_id");

        var fetched = await user.Client.GetAsync("/api/v1/tasks/" + id);
        var updated = await user.Client.PatchJsonAsync("/api/v1/tasks/" + id, ApiClient.Json(("status", "done"), ("priority", "high")));
        var deleted = await user.Client.DeleteAsync("/api/v1/tasks/" + id);
        var gone = await user.Client.GetAsync("/api/v1/tasks/" + id);

        Assert.Equal(200, fetched.Status);
        Assert.Equal(id, fetched.Text("_id"));
        Assert.Equal(200, updated.Status);
        Assert.Equal("done", updated.Text("status"));
        Assert.Equal("high", updated.Text("priority"));
        Assert.Equal("Read Atomic Habits", updated.Text("name"));
        Assert.Equal(200, deleted.Status);
        Assert.Equal("Task has been deleted", deleted.Text("message"));
        Assert.Equal(404, gone.Status);
        Assert.Equal("Task not found!", gone.Text("message"));
    }

    [Fact]
    public async Task RejectsEmptyAndInvalidUpdates()
    {
        var user = await api.RegisterAsync();
        var id = (await user.Client.PostJsonAsync("/api/v1/tasks/create", ApiFixture.TaskJson())).Text("_id");

        var empty = await user.Client.PatchJsonAsync("/api/v1/tasks/" + id, ApiClient.Json(("unknown", "x")));
        var invalid = await user.Client.PatchJsonAsync("/api/v1/tasks/" + id, ApiClient.Json(("status", "finished")));

        Assert.Equal(400, empty.Status);
        Assert.Equal("No updatable fields provided", empty.Text("message"));
        Assert.Equal(400, invalid.Status);
    }

    [Fact]
    public async Task ForbidsAccessToOtherUsersTasks()
    {
        var owner = await api.RegisterAsync();
        var intruder = await api.RegisterAsync();
        var id = (await owner.Client.PostJsonAsync("/api/v1/tasks/create", ApiFixture.TaskJson())).Text("_id");

        var read = await intruder.Client.GetAsync("/api/v1/tasks/" + id);
        var update = await intruder.Client.PatchJsonAsync("/api/v1/tasks/" + id, ApiClient.Json(("status", "done")));
        var delete = await intruder.Client.DeleteAsync("/api/v1/tasks/" + id);
        var list = await intruder.Client.GetAsync("/api/v1/tasks/user/" + owner.Id);

        Assert.Equal(403, read.Status);
        Assert.Equal(403, update.Status);
        Assert.Equal(403, delete.Status);
        Assert.Equal(403, list.Status);
        Assert.Equal(200, (await owner.Client.GetAsync("/api/v1/tasks/" + id)).Status);
    }

    [Fact]
    public async Task ReportsMalformedAndUnknownTaskIds()
    {
        var user = await api.RegisterAsync();

        var malformed = await user.Client.GetAsync("/api/v1/tasks/not-an-id");
        var unknown = await user.Client.GetAsync("/api/v1/tasks/" + Guid.NewGuid());
        var badOwner = await user.Client.GetAsync("/api/v1/tasks/user/not-an-id");

        Assert.Equal(400, malformed.Status);
        Assert.Equal("Invalid task ID format", malformed.Text("message"));
        Assert.Equal(404, unknown.Status);
        Assert.Equal(400, badOwner.Status);
    }

    [Fact]
    public async Task ListsTasksInPagesOfFour()
    {
        var user = await api.RegisterAsync();
        for (var index = 0; index < 6; index++)
        {
            await user.Client.PostJsonAsync("/api/v1/tasks/create", ApiFixture.TaskJson(name: $"task-{index}"));
        }

        var first = await user.Client.GetAsync($"/api/v1/tasks/user/{user.Id}");
        var second = await user.Client.GetAsync($"/api/v1/tasks/user/{user.Id}?page=2");
        var beyond = await user.Client.GetAsync($"/api/v1/tasks/user/{user.Id}?page=9");
        var invalidPage = await user.Client.GetAsync($"/api/v1/tasks/user/{user.Id}?page=abc");

        Assert.Equal(200, first.Status);
        Assert.Equal(4, first.Json.GetProperty("tasks").GetArrayLength());
        Assert.Equal(6, first.Json.GetProperty("taskCount").GetInt32());
        Assert.Equal(2, second.Json.GetProperty("tasks").GetArrayLength());
        Assert.Equal(0, beyond.Json.GetProperty("tasks").GetArrayLength());
        Assert.Equal(6, beyond.Json.GetProperty("taskCount").GetInt32());
        Assert.Equal(4, invalidPage.Json.GetProperty("tasks").GetArrayLength());
    }

    [Fact]
    public async Task FiltersAndSortsTheListing()
    {
        var user = await api.RegisterAsync();
        await user.Client.PostJsonAsync("/api/v1/tasks/create", ApiFixture.TaskJson(name: "charlie", status: "todo"));
        await user.Client.PostJsonAsync("/api/v1/tasks/create", ApiFixture.TaskJson(name: "alpha", status: "done"));
        await user.Client.PostJsonAsync("/api/v1/tasks/create", ApiFixture.TaskJson(name: "bravo", status: "todo"));

        var sorted = await user.Client.GetAsync($"/api/v1/tasks/user/{user.Id}?orderBy=name");
        var done = await user.Client.GetAsync($"/api/v1/tasks/user/{user.Id}?status=done");
        var unknownStatus = await user.Client.GetAsync($"/api/v1/tasks/user/{user.Id}?status=nothing");

        var names = sorted.Json.GetProperty("tasks").EnumerateArray().Select(task => task.GetProperty("name").GetString()).ToArray();
        Assert.Equal(new[] { "alpha", "bravo", "charlie" }, names);
        Assert.Equal(1, done.Json.GetProperty("taskCount").GetInt32());
        Assert.Equal("alpha", done.Json.GetProperty("tasks")[0].GetProperty("name").GetString());
        Assert.Equal(0, unknownStatus.Json.GetProperty("taskCount").GetInt32());
    }

    [Fact]
    public async Task RejectsInvalidListingFilters()
    {
        var user = await api.RegisterAsync();

        var orderBy = await user.Client.GetAsync($"/api/v1/tasks/user/{user.Id}?orderBy=owner");
        var repeated = await user.Client.GetAsync($"/api/v1/tasks/user/{user.Id}?status=a&status=b");

        Assert.Equal(400, orderBy.Status);
        Assert.Equal("Invalid orderBy field", orderBy.Text("message"));
        Assert.Equal(400, repeated.Status);
        Assert.Equal("Status filter must be a string", repeated.Text("message"));
    }

    [Fact]
    public async Task AcceptsDueDatesWithOffsetsAndDateOnlyValues()
    {
        var user = await api.RegisterAsync();

        var offset = await user.Client.PostJsonAsync("/api/v1/tasks/create", ApiFixture.TaskJson(due: "2026-03-01T07:00:00+07:00"));
        var dateOnly = await user.Client.PostJsonAsync("/api/v1/tasks/create", ApiFixture.TaskJson(due: "2026-03-01"));

        Assert.Equal("2026-03-01T00:00:00.000Z", offset.Text("due"));
        Assert.Equal("2026-03-01T00:00:00.000Z", dateOnly.Text("due"));
    }
}
