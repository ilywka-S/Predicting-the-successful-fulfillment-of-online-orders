using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;

namespace OrderSense.Api.Tests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@ordersense.test";
    public const string AdminPassword = "Admin12345";
    public const string UserPassword = "Password123";
    
    private const string JwtKey = "integration-tests-signing-key-0123456789";

    private readonly string _connectionString = TestConnectionString.CreateUnique();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:ordersense", _connectionString);
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
    }

    public Task InitializeAsync()
    {
        _ = Server;

        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }

        await DisposeAsync();
    }

    public HttpClient CreateClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    public async Task<string> CreateUserAsync(string role)
    {
        var email = $"{role}-{Guid.NewGuid():N}@ordersense.test";

        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = email, Email = email, FullName = $"Test {role}" };

        EnsureSucceeded(await userManager.CreateAsync(user, UserPassword));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, role));

        return email;
    }

    public async Task<AuthResponse> LoginAsync(string email, string password = UserPassword)
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();

        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}