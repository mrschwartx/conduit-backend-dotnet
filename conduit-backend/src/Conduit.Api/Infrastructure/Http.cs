using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Conduit.Application.Abstractions;
using Conduit.Application.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Serilog.Context;

namespace Conduit.Api.Infrastructure;

public static class ErrorWriter
{
    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext ctx, int status, IReadOnlyDictionary<string, string[]> errors)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new { errors }, Opts, ctx.RequestAborted);
    }

    public static Task WriteAsync(HttpContext ctx, int status, string key, string message) =>
        WriteAsync(ctx, status, new Dictionary<string, string[]> { [key] = [message] });
}

public sealed class JsonErrorResult(int status, IReadOnlyDictionary<string, string[]> errors) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context) => ErrorWriter.WriteAsync(context.HttpContext, status, errors);
}

/// <summary>Always "yyyy-MM-ddTHH:mm:ss.fffZ" so every backend emits the same timestamp shape.</summary>
public sealed class Iso8601UtcConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) =>
        DateTime.Parse(r.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    public override void Write(Utf8JsonWriter w, DateTime v, JsonSerializerOptions o) =>
        w.WriteStringValue(v.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
}

public static class BodyReader
{
    /// <summary>Reads the request body, enforces JSON, and returns the object stored under the envelope key.</summary>
    public static async Task<JsonElement> ReadAsync(HttpRequest req, string key, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await req.Body.CopyToAsync(ms, ct);
        if (ms.Length == 0) throw new ApiException(400, "body", "is not valid JSON");
        if (!IsJson(req.ContentType)) throw new ApiException(415, "body", "content type must be application/json");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(ms.GetBuffer().AsMemory(0, (int)ms.Length)); }
        catch (Exception e) when (e is JsonException or ArgumentException)
        { throw new ApiException(400, "body", "is not valid JSON"); }

        using (doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(key, out var inner) && inner.ValueKind == JsonValueKind.Object)
                return inner.Clone();
        }
        throw new ApiException(422, key, "can't be blank");
    }

    private static bool IsJson(string? contentType) =>
        MediaTypeHeaderValue.TryParse(contentType, out var mt) &&
        mt.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase);
}

/// <summary>X-Request-Id propagation + security headers. Uses OnStarting so the exception handler cannot wipe them.</summary>
public sealed class RequestContextMiddleware(RequestDelegate next)
{
    private static readonly Regex ValidId = new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.Compiled);

    public async Task InvokeAsync(HttpContext ctx)
    {
        var id = ctx.Request.Headers["X-Request-Id"].FirstOrDefault();
        if (id is null || !ValidId.IsMatch(id)) id = Guid.NewGuid().ToString("N");
        ctx.TraceIdentifier = id;
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h["X-Request-Id"] = id;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            h["Cache-Control"] = "no-store";
            return Task.CompletedTask;
        });
        using (LogContext.PushProperty("RequestId", id))
            await next(ctx);
    }
}

public static class TokenAuth { public const string Scheme = "Token"; }

/// <summary>Authorization: Token &lt;jwt&gt;. Any other scheme counts as an invalid token.</summary>
public sealed class TokenAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
                                     UrlEncoder encoder, ITokenService tokens)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string ErrorItem = "auth_error";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderNames.Authorization, out var header)) return AuthenticateResult.NoResult();

        var parts = header.ToString().Split(' ', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !parts[0].Equals("Token", StringComparison.OrdinalIgnoreCase) || parts[1].Length == 0)
            return Reject("is invalid");

        var result = await tokens.ValidateAsync(parts[1]);
        if (!result.IsValid) return Reject(result.Error ?? "is invalid");

        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", result.UserId.ToString(CultureInfo.InvariantCulture)),
            new Claim("username", result.Username ?? ""),
        }, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private AuthenticateResult Reject(string message)
    {
        Context.Items[ErrorItem] = message;
        return AuthenticateResult.Fail(message);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        ErrorWriter.WriteAsync(Context, 401, "token", Context.Items[ErrorItem] as string ?? "is missing");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ErrorWriter.WriteAsync(Context, 403, "user", "forbidden");
}

/// <summary>
/// Optional-auth endpoints are anonymous, but a token that is present and bad must still produce 401.
/// Endpoints with auth level NONE ignore the header entirely.
/// </summary>
public sealed class TokenGuardMiddleware(RequestDelegate next)
{
    private static readonly string[] Open = ["/health", "/api/v1/tags", "/api/v1/users", "/api/v1/users/login"];

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = (ctx.Request.Path.Value ?? "").TrimEnd('/');
        if (ctx.Request.Headers.ContainsKey(HeaderNames.Authorization) &&
            ctx.User.Identity?.IsAuthenticated != true &&
            !Open.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            await ErrorWriter.WriteAsync(ctx, 401, "token", ctx.Items[TokenAuthHandler.ErrorItem] as string ?? "is invalid");
            return;
        }
        await next(ctx);
    }
}

public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ApiException e) return;
        context.Result = new JsonErrorResult(e.Status, e.Errors);
        context.ExceptionHandled = true;
    }
}

/// <summary>Last resort: nothing but the contract error shape ever leaves the process.</summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        switch (ex)
        {
            case ApiException api:
                await ErrorWriter.WriteAsync(ctx, api.Status, api.Errors);
                break;
            case BadHttpRequestException bad:
                await ErrorWriter.WriteAsync(ctx, bad.StatusCode, "body", bad.StatusCode == 413 ? "is too large" : "is invalid");
                break;
            default:
                await ErrorWriter.WriteAsync(ctx, 500, "server", "internal server error");
                break;
        }
        return true;
    }
}
