using OrderSense.Api.Auth;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.Orders;

public static class OrderTestExtensions
{
    public static async Task<HttpClient> CreateManagerClientAsync(this ApiFactory factory)
    {
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(Roles.Manager));

        return factory.CreateClient(auth.AccessToken);
    }

    public static Task SeedAsync(this ApiFactory factory, params Order[] orders) => factory.WithDbAsync(async db =>
    {
        db.Orders.AddRange(orders);

        return await db.SaveChangesAsync();
    });
}