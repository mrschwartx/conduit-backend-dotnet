using System.Text.Json;
using Conduit.Api.Configuration;
using Conduit.Api.Infrastructure;
using Conduit.Application;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var cfg = AppConfig.FromEnvironment();   // fails fast on missing/short JWT_SECRET

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, lc) => lc
    .MinimumLevel.Is(cfg.LogLevel)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 1_048_576;   // 1 MB
    o.ListenAnyIP(cfg.Port);
});
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(15));

builder.Services.AddSingleton(cfg);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(cfg.ConnectionString, new JwtOptions(cfg.JwtSecret, cfg.JwtTtlMinutes));

builder.Services
    .AddControllers(o => o.Filters.Add<ApiExceptionFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;   // nulls are written (no ignore condition)
        o.JsonSerializerOptions.Converters.Add(new Iso8601UtcConverter());
    })
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ctx =>
        new JsonErrorResult(422, ctx.ModelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .ToDictionary(kv => JsonNamingPolicy.CamelCase.ConvertName(kv.Key),
                          kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "is invalid" : e.ErrorMessage).ToArray())));

builder.Services.AddAuthentication(TokenAuth.Scheme)
    .AddScheme<AuthenticationSchemeOptions, TokenAuthHandler>(TokenAuth.Scheme, _ => { });
builder.Services.AddAuthorization();

builder.Services.AddProblemDetails();   // required by UseExceptionHandler(); GlobalExceptionHandler always handles first
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(cfg.CorsOrigins)
    .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
    .WithHeaders("Authorization", "Content-Type")
    .WithExposedHeaders("X-Request-Id")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();   // other envs: explicit command (README)
}

app.UseMiddleware<RequestContextMiddleware>();
app.UseSerilogRequestLogging(o =>
    o.MessageTemplate = "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms");
app.UseExceptionHandler();
app.UseStatusCodePages(ctx => ctx.HttpContext.Response.StatusCode switch
{
    404 => ErrorWriter.WriteAsync(ctx.HttpContext, 404, "route", "not found"),
    405 => ErrorWriter.WriteAsync(ctx.HttpContext, 405, "method", "not allowed"),
    var s => ErrorWriter.WriteAsync(ctx.HttpContext, s, "server", "request failed"),
});
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<TokenGuardMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Text("OK"));
app.MapControllers();

app.Run();

public partial class Program;
