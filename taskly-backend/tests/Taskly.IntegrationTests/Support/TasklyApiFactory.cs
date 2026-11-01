using Microsoft.AspNetCore.Mvc.Testing;
using Taskly.Host;

namespace Taskly.IntegrationTests.Support;

public sealed class TasklyApiFactory : WebApplicationFactory<Program>
{
    public ApiClient CreateApiClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        }));
}
