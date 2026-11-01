using System.Text.Json.Serialization;
using Taskly.Modules.Users.Domain;
using Taskly.SharedKernel.Domain;

namespace Taskly.Modules.Users.Application;

public sealed record UserDto(
    [property: JsonPropertyName("_id")] string Id,
    string Username,
    string Email,
    string Avatar,
    string CreatedAt,
    string UpdatedAt)
{
    public static UserDto From(User user) => new(
        user.Id.ToString(),
        user.Username.Value,
        user.Email.Value,
        user.Avatar,
        Timestamps.Format(user.CreatedAt),
        Timestamps.Format(user.UpdatedAt));
}
