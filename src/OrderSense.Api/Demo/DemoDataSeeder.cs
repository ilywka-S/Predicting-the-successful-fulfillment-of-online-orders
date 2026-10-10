using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;

namespace OrderSense.Api.Demo;

public static class DemoDataSeeder
{
    public static async Task<int> SeedAsync(AppDbContext db, IHostEnvironment environment, int orderCount, ILogger logger, CancellationToken ct = default)
    {
        if (!environment.IsDevelopment() || orderCount <= 0 || await HasDataAsync(db, ct))
        {
            return 0;
        }

        var data = new DemoDataGenerator().Generate(orderCount);

        db.GeoZips.AddRange(data.GeoZips);
        db.Sellers.AddRange(data.Sellers);
        db.Products.AddRange(data.Products);
        db.Orders.AddRange(data.Orders);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded {Count} demo orders", orderCount);

        return orderCount;
    }

    private static async Task<bool> HasDataAsync(AppDbContext db, CancellationToken ct) => await db.Orders.AnyAsync(ct) || await db.Sellers.AnyAsync(ct) || await db.GeoZips.AnyAsync(ct);
}