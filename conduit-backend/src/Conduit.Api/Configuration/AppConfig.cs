using Microsoft.Data.SqlClient;
using Serilog.Events;

namespace Conduit.Api.Configuration;

public sealed record AppConfig(int Port, string ConnectionString, string JwtSecret, int JwtTtlMinutes,
                               string[] CorsOrigins, LogEventLevel LogLevel)
{
    public static AppConfig FromEnvironment()
    {
        static string Env(string key, string fallback = "") =>
            Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

        var secret = Env("JWT_SECRET");
        if (secret.Length < 32)
            throw new InvalidOperationException("JWT_SECRET is required and must be at least 32 characters long.");

        var cs = Env("DATABASE_CONNECTION_STRING");
        if (cs.Length == 0)
            cs = new SqlConnectionStringBuilder
            {
                DataSource = $"{Env("DATABASE_HOST", "localhost")},{Env("DATABASE_PORT", "1433")}",
                InitialCatalog = Env("DATABASE_NAME", "conduit"),
                UserID = Env("DATABASE_USER", "sa"),
                Password = Env("DATABASE_PASSWORD", "Dev_Passw0rd123"),   // matches conduit-docker/docker-compose.dev.yml
                TrustServerCertificate = bool.Parse(Env("DATABASE_TRUST_SERVER_CERTIFICATE", "true")),
                Encrypt = true,
            }.ConnectionString;

        return new AppConfig(
            int.Parse(Env("PORT", "8080")),
            cs, secret,
            int.Parse(Env("JWT_TTL_MINUTES", "720")),
            Env("CORS_ALLOWED_ORIGINS", "http://localhost:5173").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Env("LOG_LEVEL", "info").ToLowerInvariant() switch
            {
                "trace" or "verbose" => LogEventLevel.Verbose,
                "debug" => LogEventLevel.Debug,
                "warn" or "warning" => LogEventLevel.Warning,
                "error" => LogEventLevel.Error,
                "fatal" => LogEventLevel.Fatal,
                _ => LogEventLevel.Information,
            });
    }
}
