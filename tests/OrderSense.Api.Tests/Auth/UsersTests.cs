using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OrderSense.Api.Auth;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public class UsersTests(ApiFactory factory)
{
    [Fact]
    public async Task GetAll_AsAdmin_ReturnsUsersWithRoles()
    {
        var analystEmail = await factory.CreateUserAsync(Roles.Analyst);
        var client = await AdminClientAsync();

        var users = await client.GetFromJsonAsync<List<UserDto>>("/api/users");

        Assert.Contains(users!, u => u.Email == analystEmail && u.Role == Roles.Analyst);
        Assert.Contains(users!, u => u.Email == ApiFactory.AdminEmail && u.Role == Roles.Admin);
    }

    [Fact]
    public async Task GetAll_AsManager_Returns403()
    {
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(Roles.Manager));

        var response = await factory.CreateClient(auth.AccessToken).GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_CreatesUserWhoCanLogIn()
    {
        var email = $"new-{Guid.NewGuid():N}@ordersense.test";
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(email, ApiFactory.UserPassword, "New Analyst", Roles.Analyst));
        var created = await response.Content.ReadFromJsonAsync<UserDto>();
        var auth = await factory.LoginAsync(email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(email, created!.Email);
        Assert.Equal("New Analyst", created.Name);
        Assert.Equal(Roles.Analyst, auth.User.Role);
    }

    [Fact]
    public async Task Create_WithUnknownRole_Returns400()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest($"role-{Guid.NewGuid():N}@ordersense.test", ApiFactory.UserPassword, null, "superuser"));

        await AssertValidationErrorAsync(response, "Role");
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_Returns400()
    {
        var email = await factory.CreateUserAsync(Roles.Manager);
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(email, ApiFactory.UserPassword, null, Roles.Manager));

        await AssertValidationErrorAsync(response, "DuplicateEmail");
    }

    [Fact]
    public async Task Create_WithWeakPassword_Returns400()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest($"weak-{Guid.NewGuid():N}@ordersense.test", "short", null, Roles.Manager));

        await AssertValidationErrorAsync(response, "PasswordTooShort");
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var auth = await factory.LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);

        return factory.CreateClient(auth.AccessToken);
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string key)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Contains(key, problem!.Errors.Keys);
    }
}