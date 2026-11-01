using System.Globalization;
using System.Text.RegularExpressions;
using Taskly.Modules.Tasks.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Tasks.Application;

public sealed partial class ListTasksByUser(ITaskRepository tasks)
{
    public const int PageSize = 4;

    public const int MaxPage = 1_000_000;

    public async Task<TaskListDto> ExecuteAsync(
        Guid requesterId,
        string ownerId,
        object? status,
        object? orderBy,
        object? page,
        CancellationToken cancellationToken = default)
    {
        var owner = EntityIds.Parse(ownerId, "user");
        if (owner != requesterId)
        {
            throw new ForbiddenException("You are not authorized to view these tasks");
        }

        if (status is not null && status is not string)
        {
            throw new ValidationException("Status filter must be a string");
        }

        if (orderBy is not null && !(orderBy is string candidate && TaskSortFields.IsAllowed(candidate)))
        {
            throw new ValidationException("Invalid orderBy field");
        }

        var statusFilter = status is string text && text.Length > 0 ? text : null;
        var result = await tasks.FindByOwnerAsync(
            owner,
            statusFilter,
            orderBy as string,
            NormalizePage(page),
            PageSize,
            cancellationToken);

        return new TaskListDto(result.Tasks.Select(TaskDto.From).ToList(), result.Total);
    }

    public static int NormalizePage(object? page)
    {
        if (page is null)
        {
            return 1;
        }

        var text = page is string[] values ? string.Join(",", values) : Convert.ToString(page, CultureInfo.InvariantCulture) ?? string.Empty;
        var match = LeadingInteger().Match(text);
        if (!match.Success)
        {
            return 1;
        }

        var digits = match.Groups[1].Value;
        if (!long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
        {
            return digits.StartsWith('-') ? 1 : MaxPage;
        }

        return (int)Math.Min(Math.Max(parsed, 1), MaxPage);
    }

    [GeneratedRegex(@"^\s*([+-]?\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingInteger();
}
