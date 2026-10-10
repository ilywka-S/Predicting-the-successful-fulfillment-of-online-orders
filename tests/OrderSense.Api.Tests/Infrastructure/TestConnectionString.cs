using Microsoft.Extensions.Configuration;
using Npgsql;

namespace OrderSense.Api.Tests.Infrastructure;

public static class TestConnectionString
{
    private const string UserSecretsId = "ordersense-api-tests";
    
    public static string CreateUnique()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(UserSecretsId)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("TestPostgres") ?? throw new InvalidOperationException("Set ConnectionStrings:TestPostgres in the test project's user-secrets or the ConnectionStrings__TestPostgres environment variable");

        return new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = $"ordersense_test_{Guid.NewGuid():N}",
        }.ConnectionString;
    }
}