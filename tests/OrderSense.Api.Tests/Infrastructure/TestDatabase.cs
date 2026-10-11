using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;

namespace OrderSense.Api.Tests.Infrastructure;

public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _connectionString = TestConnectionString.CreateUnique();

    public static async Task<TestDatabase> CreateAsync()
    {
        var database = new TestDatabase();

        await using var db = database.CreateContext();
        await db.Database.MigrateAsync();

        return database;
    }

    public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_connectionString)
        .UseSnakeCaseNamingConvention()
        .Options);

    public async ValueTask DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }
}