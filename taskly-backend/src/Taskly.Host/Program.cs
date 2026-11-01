using Microsoft.Extensions.FileProviders;
using Taskly.Host.Configuration;
using Taskly.Host.Seeding;
using Taskly.Modules.Tasks;
using Taskly.Modules.Users;
using Taskly.SharedKernel;
using Taskly.SharedKernel.Infrastructure.Http;
using Taskly.SharedKernel.Infrastructure.Persistence;

namespace Taskly.Host;

public sealed class Program
{
    private const string CorsPolicyName = "taskly-client";

    private const string SeedFlag = "--seed";

    public static async Task<int> Main(string[] args)
    {
        WebApplication app;
        AppSettings settings;
        try
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddDotEnvFile(".env", args);
            settings = AppSettings.From(builder.Configuration);
            ConfigureServices(builder, settings);
            app = builder.Build();
        }
        catch (ConfigurationException error)
        {
            await Console.Error.WriteLineAsync(error.Message);
            return 1;
        }

        await using (app)
        {
            if (args.Contains(SeedFlag))
            {
                var exitCode = await app.Services.GetRequiredService<SeedCommand>().RunAsync();
                return exitCode;
            }

            ConfigurePipeline(app, settings);
            await app.RunAsync();
            return 0;
        }
    }

    private static void ConfigureServices(WebApplicationBuilder builder, AppSettings settings)
    {
        builder.WebHost.UseUrls($"http://{settings.Host}:{settings.Port}");

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z' ";
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddFilter("Taskly", settings.LogLevel);
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);

        var connectionString = PostgresConnectionString.Build(
            settings.DatabaseUrl,
            settings.DatabasePoolMax,
            settings.DatabaseTimeoutMs);

        builder.Services.AddSingleton(settings);
        builder.Services.AddSharedKernel(connectionString);
        builder.Services.AddUsersModule(settings.ToUsersOptions());
        builder.Services.AddTasksModule();
        builder.Services.AddSingleton<SeedCommand>();
        builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
            .WithOrigins(settings.ClientUrl)
            .AllowCredentials()
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH")
            .WithHeaders("Content-Type", "Authorization")));
    }

    private static void ConfigurePipeline(WebApplication app, AppSettings settings)
    {
        Directory.CreateDirectory(settings.PublicDirectory);

        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseCors(CorsPolicyName);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(settings.PublicDirectory),
            OnPrepareResponse = context => context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff"
        });
        app.UseRouting();
        app.UseMiddleware<AccessTokenMiddleware>();

        app.MapGet("/", () => Results.Ok(new MessageResponse("Welcome to the Taskly API")));
        app.MapHealthChecks("/health");
        app.MapUsersEndpoints();
        app.MapTasksEndpoints();
        app.MapFallback("{*path}", () => Results.Json(
            new MessageResponse("Not found"),
            statusCode: StatusCodes.Status404NotFound));
    }
}
