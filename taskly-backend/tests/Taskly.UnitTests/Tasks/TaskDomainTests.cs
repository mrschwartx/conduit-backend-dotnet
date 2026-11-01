using Taskly.Modules.Tasks.Domain;
using Taskly.SharedKernel.Domain;
using Taskly.UnitTests.Support;

namespace Taskly.UnitTests.Tasks;

public sealed class TaskDomainTests
{
    private static TaskItem BuildTask() => TaskItem.Create(
        Ids.Task,
        Ids.User,
        TaskName.Create("Read Atomic Habits"),
        TaskDescription.Create("Finish the book"),
        TaskPriority.Low,
        DueDate.Create("2026-12-08T10:00:00Z"),
        TaskItemStatus.Todo,
        Ids.FixedNow);

    [Fact]
    public void CreatesTasksWithEqualTimestamps()
    {
        var task = BuildTask();

        Assert.Equal(Ids.FixedNow, task.CreatedAt);
        Assert.Equal(task.CreatedAt, task.UpdatedAt);
        Assert.True(task.IsOwnedBy(Ids.User));
        Assert.False(task.IsOwnedBy(Ids.OtherUser));
    }

    [Fact]
    public void AppliesOnlyTheProvidedChanges()
    {
        var task = BuildTask();
        var later = Ids.FixedNow.AddHours(2);

        task.Update(new TaskChanges(TaskName.Create("Renamed"), null, null, null, TaskItemStatus.Done), later);

        Assert.Equal("Renamed", task.Name.Value);
        Assert.Equal("Finish the book", task.Description.Value);
        Assert.Equal("low", task.Priority.Value);
        Assert.Equal("done", task.Status.Value);
        Assert.Equal(later, task.UpdatedAt);
        Assert.Equal(Ids.FixedNow, task.CreatedAt);
    }

    [Fact]
    public void DetectsEmptyChangeSets()
    {
        Assert.True(new TaskChanges(null, null, null, null, null).IsEmpty);
        Assert.False(new TaskChanges(null, TaskDescription.Empty, null, null, null).IsEmpty);
    }

    [Fact]
    public void ValidatesNames()
    {
        Assert.Equal("Name", TaskName.Create("  Name ").Value);
        Assert.Equal(120, TaskName.Create(new string('a', 120)).Value.Length);
        foreach (var value in new object?[] { null, 5, "   " , new string('a', 121) })
        {
            Assert.Throws<ValidationException>(() => TaskName.Create(value));
        }

        var missing = Assert.Throws<ValidationException>(() => TaskName.Create(null));
        Assert.Equal("Task name is required", missing.Message);
    }

    [Fact]
    public void ValidatesDescriptions()
    {
        Assert.Equal(string.Empty, TaskDescription.Create("   ").Value);
        Assert.Equal(2000, TaskDescription.Create(new string('a', 2000)).Value.Length);
        var tooLong = Assert.Throws<ValidationException>(() => TaskDescription.Create(new string('a', 2001)));
        var missing = Assert.Throws<ValidationException>(() => TaskDescription.Create(null));

        Assert.Equal("Task description must be between 0 and 2000 characters", tooLong.Message);
        Assert.Equal("Task description is required", missing.Message);
    }

    [Fact]
    public void ValidatesPriorities()
    {
        Assert.Same(TaskPriority.High, TaskPriority.Create(" high "));
        Assert.Same(TaskPriority.Medium, TaskPriority.Restore("medium"));
        foreach (var value in new object?[] { "urgent", "HIGH", null, 3 })
        {
            Assert.Throws<ValidationException>(() => TaskPriority.Create(value));
        }

        var error = Assert.Throws<ValidationException>(() => TaskPriority.Create("urgent"));
        Assert.Equal("Task priority must be one of: low, medium, high", error.Message);
        Assert.Throws<ArgumentException>(() => TaskPriority.Restore("urgent"));
    }

    [Fact]
    public void ValidatesStatuses()
    {
        Assert.Same(TaskItemStatus.InProgress, TaskItemStatus.Create("in_progress"));
        Assert.Same(TaskItemStatus.Todo, TaskItemStatus.Default);
        Assert.Same(TaskItemStatus.Done, TaskItemStatus.Restore("done"));
        var error = Assert.Throws<ValidationException>(() => TaskItemStatus.Create("finished"));
        Assert.Equal("Task status must be one of: todo, in_progress, done", error.Message);
        Assert.Throws<ArgumentException>(() => TaskItemStatus.Restore("finished"));
    }

    [Fact]
    public void ListsTheSortableFields()
    {
        Assert.True(TaskSortFields.IsAllowed("createdAt"));
        Assert.True(TaskSortFields.IsAllowed("due"));
        Assert.False(TaskSortFields.IsAllowed("created_at"));
        Assert.False(TaskSortFields.IsAllowed("owner"));
    }
}

public sealed class DueDateTests
{
    [Theory]
    [InlineData("2026-12-08T10:00:00Z", "2026-12-08T10:00:00.000Z")]
    [InlineData("2026-03-01T07:00:00+07:00", "2026-03-01T00:00:00.000Z")]
    [InlineData("2026-03-01T10:00:00", "2026-03-01T10:00:00.000Z")]
    [InlineData("2026-03-01T10:00", "2026-03-01T10:00:00.000Z")]
    [InlineData("2026-01-01T00:00Z", "2026-01-01T00:00:00.000Z")]
    [InlineData("2026-03-01", "2026-03-01T00:00:00.000Z")]
    [InlineData("  2026-03-01  ", "2026-03-01T00:00:00.000Z")]
    [InlineData("2026-03-01T10:00:00.123456Z", "2026-03-01T10:00:00.123Z")]
    [InlineData("Tue, 3 Jun 2008 11:05:30 GMT", "2008-06-03T11:05:30.000Z")]
    public void ParsesSupportedFormatsIntoUtc(string input, string expected)
    {
        Assert.Equal(expected, Timestamps.Format(DueDate.Create(input).Value));
    }

    [Fact]
    public void RejectsMissingValues()
    {
        foreach (var value in new object?[] { null, 5, string.Empty, "   " })
        {
            var error = Assert.Throws<ValidationException>(() => DueDate.Create(value));
            Assert.Equal("Task due date is required", error.Message);
        }
    }

    [Theory]
    [InlineData("someday")]
    [InlineData("2026-13-45")]
    [InlineData("2026-02-30")]
    [InlineData("+010000-01-01T00:00:00Z")]
    [InlineData("0000-01-01T00:00:00Z")]
    public void RejectsUnparseableValues(string input)
    {
        var error = Assert.Throws<ValidationException>(() => DueDate.Create(input));
        Assert.Equal("Task due date is invalid", error.Message);
    }

    [Fact]
    public void RestoresStoredValues()
    {
        Assert.Equal(Ids.FixedNow, DueDate.Restore(Ids.FixedNow).Value);
    }
}
