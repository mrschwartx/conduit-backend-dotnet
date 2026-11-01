namespace Taskly.SharedKernel.Infrastructure.Http;

public sealed class RequireAccessTokenMetadata
{
    public static readonly RequireAccessTokenMetadata Instance = new();

    private RequireAccessTokenMetadata()
    {
    }
}
