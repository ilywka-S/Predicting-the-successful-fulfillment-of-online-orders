using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using OrderSense.Api.Demo;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.Demo;

public class DemoDataSeederTests
{
    private static readonly IHostEnvironment Development = new HostingEnvironment { EnvironmentName = Environments.Development };
    private static readonly IHostEnvironment Production = new HostingEnvironment { EnvironmentName = Environments.Production };

    [Fact]
    public async Task Seed_EmptyDatabaseInDevelopment_CreatesRequestedOrders()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        
        var seeded = await DemoDataSeeder.SeedAsync(db, Development, 200, NullLogger.Instance);

        await using var check = database.CreateContext();
        Assert.Equal(200, seeded);
        Assert.Equal(200, await check.Orders.CountAsync());
        Assert.Equal(200, await check.OrderPayments.CountAsync());
        Assert.True(await check.OrderItems.CountAsync() >= 200);
    }

    [Fact]
    public async Task Seed_SecondRun_DoesNotDuplicate()
    {
        await using var database = await TestDatabase.CreateAsync();

        await using (var first = database.CreateContext())
        {
            await DemoDataSeeder.SeedAsync(first, Development, 30, NullLogger.Instance);
        }

        await using var second = database.CreateContext();
        var seeded = await DemoDataSeeder.SeedAsync(second, Development, 30, NullLogger.Instance);

        Assert.Equal(0, seeded);
        Assert.Equal(30, await second.Orders.CountAsync());
    }

    [Fact]
    public async Task Seed_InProduction_DoesNothing()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        var seeded = await DemoDataSeeder.SeedAsync(db, Production, 30, NullLogger.Instance);

        Assert.Equal(0, seeded);
        Assert.Equal(0, await db.Orders.CountAsync());
    }

    [Fact]
    public async Task Seed_ZeroOrders_DoesNothing()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.CreateContext();

        var seeded = await DemoDataSeeder.SeedAsync(db, Development, 0, NullLogger.Instance);

        Assert.Equal(0, seeded);
        Assert.Equal(0, await db.Orders.CountAsync());
    }
}