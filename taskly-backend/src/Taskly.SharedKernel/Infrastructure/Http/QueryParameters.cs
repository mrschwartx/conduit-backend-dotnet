using Microsoft.AspNetCore.Http;

namespace Taskly.SharedKernel.Infrastructure.Http;

public static class QueryParameters
{
    public static object? Value(IQueryCollection query, string key)
    {
        if (!query.TryGetValue(key, out var values) || values.Count == 0)
        {
            return null;
        }

        return values.Count == 1 ? values[0] : values.ToArray();
    }
}
