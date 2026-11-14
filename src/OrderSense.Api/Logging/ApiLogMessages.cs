using Microsoft.Extensions.Logging;

namespace OrderSense.Api.Logging;

internal static partial class Log
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Reuse of revoked refresh token for user {UserId}; revoking all sessions")]
    public static partial void RevokedRefreshTokenReused(this ILogger logger, Guid userId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Admin user {Email} created")]
    public static partial void AdminUserCreated(this ILogger logger, string email);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Simulator could not create an order")]
    public static partial void SimulatorOrderCreationFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Simulator needs sellers and products in the database")]
    public static partial void SimulatorMissingSeedData(this ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Seeded {Count} demo orders")]
    public static partial void DemoOrdersSeeded(this ILogger logger, int count);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Hangfire needs a direct database endpoint: '-pooler' was removed from the host name")]
    public static partial void HangfirePoolerRemoved(this ILogger logger);
}
