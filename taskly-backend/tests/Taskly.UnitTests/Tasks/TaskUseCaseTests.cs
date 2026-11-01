using Taskly.Modules.Tasks.Application;
using Taskly.SharedKernel.Application;
using Taskly.SharedKernel.Domain;
using Taskly.UnitTests.Support;

namespace Taskly.UnitTests.Tasks;

public sealed class TaskUseCaseTests
{
    private readonly TasksFixture fixture = new();

    [Fact]
    public async Task CreatesATaskForTheOwner()
    {
        var created = await fixture.Create.ExecuteAsync(Ids.User, TasksFixture.Payload());

        Assert.Equal("Read Atomic Habits", created.Name);
        Assert.Equal("low", created.Priority);
        Assert.Equal("todo", created.Status);
        Assert.Equal(Ids.User.ToString(), created.Owner);
        Assert.Equal("2026-12-08T10:00:00.000Z", created.Due);
        Assert.Equal("2026-01-01T00:00:00.000Z", created.CreatedAt);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);
        Assert.Single(fixture.Tasks.Tasks);
    }

    [Fact]
    public async Task DefaultsTheDescriptionAndStatusWhenAbsent()
    {
        var created = await fixture.Create.ExecuteAsync(
            Ids.User,
            TasksFixture.Payload(description: null, status: null));

        Assert.Equal(string.Empty, created.Description);
        Assert.Equal("todo", created.Status);
    }

    [Fact]
    public async Task TreatsExplicitNullsAsInvalid()
    {
        var withNullDescription = new Dictionary<string, object?>(TasksFixture.Payload()) { ["description"] = null };
        var withNullStatus = new Dictionary<string, object?>(TasksFixture.Payload()) { ["status"] = null };

        var description = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Create.ExecuteAsync(Ids.User, withNullDescription));
        var status = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Create.ExecuteAsync(Ids.User, withNullStatus));

        Assert.Equal("Task description is required", description.Message);
        Assert.Equal("Task status is required", status.Message);
    }

    [Fact]
    public async Task RejectsInvalidPayloadsWithoutStoringAnything()
    {
        var payloads = new[]
        {
            TasksFixture.Payload(name: null),
            TasksFixture.Payload(name: new string('a', 121)),
            TasksFixture.Payload(priority: "urgent"),
            TasksFixture.Payload(priority: null),
            TasksFixture.Payload(due: "someday"),
            TasksFixture.Payload(due: null),
            TasksFixture.Payload(status: "finished")
        };

        foreach (var payload in payloads)
        {
            await Assert.ThrowsAsync<ValidationException>(() => fixture.Create.ExecuteAsync(Ids.User, payload));
        }

        Assert.Empty(fixture.Tasks.Tasks);
    }

    [Fact]
    public async Task GetsOwnTasksOnly()
    {
        var task = await fixture.MakeAsync();

        var found = await fixture.Get.ExecuteAsync(Ids.User, task.Id.ToUpperInvariant());
        var foreign = await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Get.ExecuteAsync(Ids.OtherUser, task.Id));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => fixture.Get.ExecuteAsync(Ids.User, Ids.Missing));
        var malformed = await Assert.ThrowsAsync<ValidationException>(() => fixture.Get.ExecuteAsync(Ids.User, "nope"));

        Assert.Equal(task.Id, found.Id);
        Assert.Equal("You are not authorized to view this task", foreign.Message);
        Assert.Equal("Task not found!", missing.Message);
        Assert.Equal("Invalid task ID format", malformed.Message);
    }

    [Fact]
    public async Task UpdatesTheProvidedFields()
    {
        var task = await fixture.MakeAsync();
        fixture.Clock.Advance(TimeSpan.FromHours(1));

        var updated = await fixture.Update.ExecuteAsync(
            Ids.User,
            task.Id,
            Input.Of(("status", "done"), ("priority", "high"), ("owner", Ids.OtherUser.ToString())));

        Assert.Equal("done", updated.Status);
        Assert.Equal("high", updated.Priority);
        Assert.Equal(task.Name, updated.Name);
        Assert.Equal(Ids.User.ToString(), updated.Owner);
        Assert.NotEqual(task.UpdatedAt, updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateRejectsEmptyForeignInvalidAndUnknownTasks()
    {
        var task = await fixture.MakeAsync();

        var empty = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Update.ExecuteAsync(Ids.User, task.Id, Input.Of(("unknown", "x"))));
        var foreign = await Assert.ThrowsAsync<ForbiddenException>(() =>
            fixture.Update.ExecuteAsync(Ids.OtherUser, task.Id, Input.Of(("status", "done"))));
        var invalid = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Update.ExecuteAsync(Ids.User, task.Id, Input.Of(("status", "finished"))));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Update.ExecuteAsync(Ids.User, Ids.Missing, Input.Of(("status", "done"))));

        Assert.Equal("No updatable fields provided", empty.Message);
        Assert.Equal("You are not authorized to update this task", foreign.Message);
        Assert.Contains("Task status must be one of", invalid.Message);
        Assert.Equal("Task not found!", missing.Message);
    }

    [Fact]
    public async Task DeletesOwnTasksOnly()
    {
        var task = await fixture.MakeAsync();

        var foreign = await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Delete.ExecuteAsync(Ids.OtherUser, task.Id));
        await fixture.Delete.ExecuteAsync(Ids.User, task.Id);
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => fixture.Delete.ExecuteAsync(Ids.User, task.Id));

        Assert.Equal("You are not authorized to delete this task", foreign.Message);
        Assert.Equal("Task not found!", missing.Message);
        Assert.Empty(fixture.Tasks.Tasks);
    }

    [Fact]
    public async Task DeletesEveryTaskOfAnOwner()
    {
        await fixture.MakeAsync(Ids.User, "one");
        await fixture.MakeAsync(Ids.User, "two");
        await fixture.MakeAsync(Ids.OtherUser, "other");

        var removed = await fixture.DeleteByOwner.ExecuteAsync(Ids.User);

        Assert.Equal(2, removed);
        Assert.Equal(Ids.OtherUser, Assert.Single(fixture.Tasks.Tasks.Values).OwnerId);
    }

    [Fact]
    public async Task UserDeletedEventsRemoveTheOwnersTasks()
    {
        await fixture.MakeAsync(Ids.User);
        var handler = new UserDeletedHandler(fixture.DeleteByOwner);

        await handler.HandleAsync(new UserDeleted(Ids.User, Ids.FixedNow), CancellationToken.None);

        Assert.Empty(fixture.Tasks.Tasks);
    }
}

public sealed class ListTasksByUserTests
{
    private readonly TasksFixture fixture = new();

    [Fact]
    public async Task PagesTasksFourAtATimeAndReportsTheTotal()
    {
        for (var index = 0; index < 9; index++)
        {
            await fixture.MakeAsync(name: $"task-{index}");
        }

        var first = await fixture.List.ExecuteAsync(Ids.User, Ids.User.ToString(), null, null, null);
        var third = await fixture.List.ExecuteAsync(Ids.User, Ids.User.ToString(), null, null, "3");
        var beyond = await fixture.List.ExecuteAsync(Ids.User, Ids.User.ToString(), null, null, "99");

        Assert.Equal(4, first.Tasks.Count);
        Assert.Equal(9, first.TaskCount);
        Assert.Equal("task-0", first.Tasks[0].Name);
        Assert.Single(third.Tasks);
        Assert.Equal("task-8", third.Tasks[0].Name);
        Assert.Empty(beyond.Tasks);
        Assert.Equal(9, beyond.TaskCount);
    }

    [Fact]
    public async Task FiltersByStatusAndIgnoresBlankFilters()
    {
        await fixture.MakeAsync(name: "a", status: "todo");
        await fixture.MakeAsync(name: "b", status: "done");

        var done = await fixture.List.ExecuteAsync(Ids.User, Ids.User.ToString(), "done", null, null);
        var blank = await fixture.List.ExecuteAsync(Ids.User, Ids.User.ToString(), string.Empty, null, null);
        var unknown = await fixture.List.ExecuteAsync(Ids.User, Ids.User.ToString(), "nothing", null, null);

        Assert.Equal("b", Assert.Single(done.Tasks).Name);
        Assert.Equal(2, blank.TaskCount);
        Assert.Equal(0, unknown.TaskCount);
    }

    [Fact]
    public async Task SortsByAnAllowedField()
    {
        await fixture.MakeAsync(name: "c");
        await fixture.MakeAsync(name: "a");
        await fixture.MakeAsync(name: "b");

        var sorted = await fixture.List.ExecuteAsync(Ids.User, Ids.User.ToString(), null, "name", null);

        Assert.Equal(new[] { "a", "b", "c" }, sorted.Tasks.Select(task => task.Name).ToArray());
    }

    [Fact]
    public async Task OnlyListsTheRequestersOwnTasks()
    {
        await fixture.MakeAsync(Ids.User, "mine");
        await fixture.MakeAsync(Ids.OtherUser, "theirs");

        var foreign = await Assert.ThrowsAsync<ForbiddenException>(() =>
            fixture.List.ExecuteAsync(Ids.User, Ids.OtherUser.ToString(), null, null, null));
        var malformed = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.List.ExecuteAsync(Ids.User, "nope", null, null, null));
        var own = await fixture.List.ExecuteAsync(Ids.User, Ids.User.ToString().ToUpperInvariant(), null, null, null);

        Assert.Equal("You are not authorized to view these tasks", foreign.Message);
        Assert.Equal("Invalid user ID format", malformed.Message);
        Assert.Equal("mine", Assert.Single(own.Tasks).Name);
    }

    [Fact]
    public async Task RejectsInvalidFilters()
    {
        var ownerId = Ids.User.ToString();

        var status = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.List.ExecuteAsync(Ids.User, ownerId, new[] { "a", "b" }, null, null));
        var unknownField = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.List.ExecuteAsync(Ids.User, ownerId, null, "owner", null));
        var emptyField = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.List.ExecuteAsync(Ids.User, ownerId, null, string.Empty, null));

        Assert.Equal("Status filter must be a string", status.Message);
        Assert.Equal("Invalid orderBy field", unknownField.Message);
        Assert.Equal("Invalid orderBy field", emptyField.Message);
    }

    [Fact]
    public void NormalizesPageNumbers()
    {
        Assert.Equal(1, ListTasksByUser.NormalizePage(null));
        Assert.Equal(3, ListTasksByUser.NormalizePage("3"));
        Assert.Equal(5, ListTasksByUser.NormalizePage("+5"));
        Assert.Equal(7, ListTasksByUser.NormalizePage("  7"));
        Assert.Equal(2, ListTasksByUser.NormalizePage("2abc"));
        Assert.Equal(2, ListTasksByUser.NormalizePage(new[] { "2", "5" }));
        Assert.Equal(1, ListTasksByUser.NormalizePage("0"));
        Assert.Equal(1, ListTasksByUser.NormalizePage("-4"));
        Assert.Equal(1, ListTasksByUser.NormalizePage("abc"));
        Assert.Equal(1, ListTasksByUser.NormalizePage(string.Empty));
        Assert.Equal(ListTasksByUser.MaxPage, ListTasksByUser.NormalizePage("999999999"));
        Assert.Equal(ListTasksByUser.MaxPage, ListTasksByUser.NormalizePage("99999999999999999999"));
        Assert.Equal(1, ListTasksByUser.NormalizePage("-99999999999999999999"));
    }
}
