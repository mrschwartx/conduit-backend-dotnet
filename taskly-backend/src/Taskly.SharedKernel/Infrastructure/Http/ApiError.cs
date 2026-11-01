namespace Taskly.SharedKernel.Infrastructure.Http;

public sealed record ApiError(int Status, string Message);
