using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrderSense.Api.Auth;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public class IdentitySeederTests(ApiFactory factory)
{
    [Fact]
    public async Task Startup_SeedsRolesAndAdmin()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = await factory.WithDbAsync(db => db.Roles.Select(r => r.Name).ToListAsync());

        var admin = await userManager.FindByEmailAsync(ApiFactory.AdminEmail);

        Assert.Equal(Roles.All.Order(), roles.Order());
        Assert.NotNull(admin);
        Assert.True(await userManager.IsInRoleAsync(admin, Roles.Admin));
    }

    [Fact]
    public async Task SeedAsync_RunAgain_DoesNotDuplicateRolesOrAdmin()
    {
        await SeedAsync(factory.Services.GetRequiredService<IConfiguration>());

        var roles = await factory.WithDbAsync(db => db.Roles.CountAsync());
        var admins = await factory.WithDbAsync(db => db.Users.CountAsync(u => u.Email == ApiFactory.AdminEmail));

        Assert.Equal(Roles.All.Length, roles);
        Assert.Equal(1, admins);
    }

    [Fact]
    public async Task SeedAsync_WithoutAdminSettings_CreatesNoUsers()
    {
        var before = await factory.WithDbAsync(db => db.Users.CountAsync());

        await SeedAsync(new ConfigurationBuilder().Build());

        var after = await factory.WithDbAsync(db => db.Users.CountAsync());

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task SeedAsync_WithInvalidAdminPassword_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminEmail"] = $"admin-{Guid.NewGuid():N}@ordersense.test",
                ["Seed:AdminPassword"] = "123"
            })
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(configuration));
    }

    private async Task SeedAsync(IConfiguration configuration)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        await IdentitySeeder.SeedAsync(scope.ServiceProvider, configuration, NullLogger.Instance);
    }
}