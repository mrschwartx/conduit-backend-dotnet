using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Taskly.SharedKernel.Domain;
using Taskly.SharedKernel.Infrastructure.Http;

namespace Taskly.UnitTests.SharedKernel;

public sealed class ErrorHandlingMiddlewareTests
{
    [Theory]
    [InlineData("VALIDATION_ERROR", 400)]
    [InlineData("NOT_FOUND", 404)]
    [InlineData("ALREADY_EXISTS", 422)]
    [InlineData("AUTHENTICATION_FAILED", 401)]
    [InlineData("FORBIDDEN", 403)]
    [InlineData("PAYLOAD_TOO_LARGE", 413)]
    public async Task MapsDomainErrorsToStatusCodes(string code, int expected)
    {
        DomainException error = code switch
        {
            "VALIDATION_ERROR" => new ValidationException("message"),
            "NOT_FOUND" => new NotFoundException("message"),
            "ALREADY_EXISTS" => new AlreadyExistsException("message"),
            "AUTHENTICATION_FAILED" => new AuthenticationFailedException("message"),
            "FORBIDDEN" => new ForbiddenException("message"),
            _ => new PayloadTooLargeException("message")
        };

        var (status, body) = await RunAsync(error);

        Assert.Equal(expected, status);
        Assert.Equal(expected, body.GetProperty("status").GetInt32());
        Assert.Equal("message", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task HidesUnexpectedErrors()
    {
        var (status, body) = await RunAsync(new InvalidOperationException("secret detail"));

        Assert.Equal(500, status);
        Assert.Equal(ErrorHandlingMiddleware.DefaultMessage, body.GetProperty("message").GetString());
        Assert.DoesNotContain("secret", body.ToString());
    }

    [Fact]
    public async Task PassesSuccessfulRequestsThrough()
    {
        var context = new DefaultHttpContext();
        var middleware = new ErrorHandlingMiddleware(
            innerContext =>
            {
                innerContext.Response.StatusCode = 204;
                return Task.CompletedTask;
            },
            NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(204, context.Response.StatusCode);
    }

    private static async Task<(int Status, JsonElement Body)> RunAsync(Exception error)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ErrorHandlingMiddleware(_ => throw error, NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, document.RootElement.Clone());
    }
}
