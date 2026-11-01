using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Taskly.Modules.Users;

namespace Taskly.Host.Configuration;

public sealed partial record AppSettings(
    string Name,
    string Host,
    int Port,
    string Level,
    string DatabaseUrl,
    int DatabasePoolMax,
    int DatabaseTimeoutMs,
    string AuthSecret,
    string? CookieDomain,
    int BcryptRounds,
    string ClientUrl,
    LogLevel LogLevel,
    string PublicDirectory,
    bool SeedAllowProduction)
{
    public const int MinSecretLength = 32;

    public const int MinBcryptRounds = 4;

    public const int MaxBcryptRounds = 31;

    public const string CookieName = "taskly_token";

    public const long AvatarMaxBytes = 2 * 1024 * 1024;

    public static readonly TimeSpan TokenTtl = TimeSpan.FromHours(1);

    public bool IsProduction => Level == "production";

    public static AppSettings From(IConfiguration configuration)
    {
        var level = Text(configuration, "APP_LEVEL") ?? "development";
        var production = level == "production";

        var secret = Required(configuration, "AUTH_SECRET");
        if (secret.Length < MinSecretLength)
        {
            throw new ConfigurationException($"AUTH_SECRET must be at least {MinSecretLength} characters long");
        }

        var rounds = ToInteger(Text(configuration, "BCRYPT_ROUNDS"), 10);
        if (rounds < MinBcryptRounds || rounds > MaxBcryptRounds)
        {
            throw new ConfigurationException($"BCRYPT_ROUNDS must be between {MinBcryptRounds} and {MaxBcryptRounds}");
        }

        return new AppSettings(
            Text(configuration, "APP_NAME") ?? "taskly-api",
            Text(configuration, "APP_HOST") ?? "0.0.0.0",
            ToInteger(Text(configuration, "APP_PORT"), 8080),
            level,
            Required(configuration, "DATABASE_URL"),
            ToInteger(Text(configuration, "DATABASE_POOL_MAX"), 10),
            ToInteger(Text(configuration, "DATABASE_TIMEOUT"), 30000),
            secret,
            Text(configuration, "COOKIE_DOMAIN"),
            rounds,
            (Text(configuration, "CLIENT_URL") ?? "http://localhost:3000").TrimEnd('/'),
            ToLogLevel(Text(configuration, "LOG_LEVEL"), production ? LogLevel.Information : LogLevel.Debug),
            Path.GetFullPath(Text(configuration, "PUBLIC_DIR") ?? "public"),
            ToBoolean(Text(configuration, "SEED_ALLOW_PRODUCTION"), false));
    }

    public UsersModuleOptions ToUsersOptions() => new(
        AuthSecret,
        TokenTtl,
        BcryptRounds,
        CookieName,
        CookieDomain,
        IsProduction,
        PublicDirectory,
        AvatarMaxBytes);

    public static int ToInteger(string? value, int fallback)
    {
        if (value is null)
        {
            return fallback;
        }

        var match = IntegerPrefix().Match(value);
        return match.Success && int.TryParse(match.Value, out var parsed) ? parsed : fallback;
    }

    public static bool ToBoolean(string? value, bool fallback)
    {
        if (string.IsNullOrEmpty(value))
        {
            return fallback;
        }

        return value.ToLowerInvariant() is "1" or "true" or "yes";
    }

    public static LogLevel ToLogLevel(string? value, LogLevel fallback) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" or "information" => LogLevel.Information,
            "warn" or "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" or "fatal" => LogLevel.Critical,
            _ => fallback
        };

    private static string? Text(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string Required(IConfiguration configuration, string key) =>
        Text(configuration, key) ?? throw new ConfigurationException($"Missing required environment variable: {key}");

    [GeneratedRegex(@"^\s*[+-]?\d+", RegexOptions.CultureInvariant)]
    private static partial Regex IntegerPrefix();
}
