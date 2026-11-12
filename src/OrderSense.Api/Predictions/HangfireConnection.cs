using Npgsql;

namespace OrderSense.Api.Predictions;

public sealed record HangfireConnectionInfo(string ConnectionString, bool PoolerRemoved);

public static class HangfireConnection
{
    // Hangfire потребує прямого підключення: pooled-ендпоінт Neon (pgbouncer) ламає блокування й довгі з'єднання
    public static HangfireConnectionInfo Resolve(IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("hangfire");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration.GetConnectionString("ordersense");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'ordersense' (or 'hangfire') is not configured");
        }

        NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder(connectionString);
        string host = builder.Host ?? "";

        if (!host.Contains("-pooler", StringComparison.OrdinalIgnoreCase))
        {
            return new HangfireConnectionInfo(connectionString, false);
        }

        builder.Host = host.Replace("-pooler", "", StringComparison.OrdinalIgnoreCase);

        return new HangfireConnectionInfo(builder.ConnectionString, true);
    }
}