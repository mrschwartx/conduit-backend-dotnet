using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Taskly.SharedKernel.Domain;

namespace Taskly.SharedKernel.Infrastructure.Http;

public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public const string DefaultMessage = "We're having technical issues. Please try again later.";

    private static readonly IReadOnlyDictionary<string, int> StatusByCode = new Dictionary<string, int>
    {
        ["VALIDATION_ERROR"] = StatusCodes.Status400BadRequest,
        ["NOT_FOUND"] = StatusCodes.Status404NotFound,
        ["ALREADY_EXISTS"] = StatusCodes.Status422UnprocessableEntity,
        ["AUTHENTICATION_FAILED"] = StatusCodes.Status401Unauthorized,
        ["FORBIDDEN"] = StatusCodes.Status403Forbidden,
        ["PAYLOAD_TOO_LARGE"] = StatusCodes.Status413PayloadTooLarge
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
        }
        catch (Exception error) when (!context.Response.HasStarted)
        {
            await WriteAsync(context, error);
        }
    }

    private async Task WriteAsync(HttpContext context, Exception error)
    {
        var (status, message) = Describe(error);
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(error, "Unhandled error: {Message}", error.Message);
        }
        else
        {
            logger.LogWarning("Request rejected status={Status} message={Message}", status, message);
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ApiError(status, message));
    }

    private static (int Status, string Message) Describe(Exception error) => error switch
    {
        DomainException domain => (StatusByCode.GetValueOrDefault(domain.Code, StatusCodes.Status500InternalServerError), domain.Message),
        BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } => (StatusCodes.Status413PayloadTooLarge, "Request body is too large"),
        BadHttpRequestException bad => (bad.StatusCode, "Bad request"),
        _ => (StatusCodes.Status500InternalServerError, DefaultMessage)
    };
}
