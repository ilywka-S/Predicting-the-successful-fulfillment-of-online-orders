using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Auth;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokensAndUser()
    {
        var email = await factory.CreateUserAsync(Roles.Manager);

        var auth = await factory.LoginAsync(email);

        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.True(auth.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(email, auth.User.Email);
        Assert.Equal(Roles.Manager, auth.User.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var email = await factory.CreateUserAsync(Roles.Manager);

        var response = await PostAsync("/api/auth/login", new LoginRequest(email, "Wrong12345"));

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Invalid email or password");
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401WithSameTitle()
    {
        var response = await PostAsync("/api/auth/login", new LoginRequest("nobody@ordersense.test", "Password123"));

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Invalid email or password");
    }

    [Fact]
    public async Task Login_WithInvalidEmail_Returns400()
    {
        var response = await PostAsync("/api/auth/login", new LoginRequest("not-an-email", "Password123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_LocksAccount()
    {
        var email = await factory.CreateUserAsync(Roles.Manager);

        for (var i = 0; i < 5; i++)
        {
            await PostAsync("/api/auth/login", new LoginRequest(email, "Wrong12345"));
        }

        var response = await PostAsync("/api/auth/login", new LoginRequest(email, ApiFactory.UserPassword));

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Account is temporarily locked after too many failed attempts");
    }

    [Fact]
    public async Task Me_WithAccessToken_ReturnsCurrentUser()
    {
        var email = await factory.CreateUserAsync(Roles.Analyst);
        var auth = await factory.LoginAsync(email);

        var user = await factory.CreateClient(auth.AccessToken).GetFromJsonAsync<UserDto>("/api/auth/me");

        Assert.Equal(auth.User.Id, user!.Id);
        Assert.Equal(email, user.Email);
        Assert.Equal(Roles.Analyst, user.Role);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithValidToken_IssuesNewPairAndRevokesOldToken()
    {
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(Roles.Manager));

        var response = await PostAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        var refreshed = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(auth.RefreshToken, refreshed!.RefreshToken);
        Assert.Equal(auth.User.Id, refreshed.User.Id);

        var reused = await PostAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));

        await AssertProblemAsync(reused, HttpStatusCode.Unauthorized, "Invalid refresh token");
    }

    [Fact]
    public async Task Refresh_WithReusedToken_RevokesAllSessions()
    {
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(Roles.Manager));
        var refreshed = await (await PostAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken))).Content.ReadFromJsonAsync<AuthResponse>();

        await PostAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));
        var response = await PostAsync("/api/auth/refresh", new RefreshRequest(refreshed!.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_Returns401()
    {
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(Roles.Manager));

        await factory.WithDbAsync(db => db.RefreshTokens
            .Where(t => t.UserId == auth.User.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1))));

        var response = await PostAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Returns401()
    {
        var response = await PostAsync("/api/auth/refresh", new RefreshRequest("unknown-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(Roles.Manager));

        var logout = await PostAsync("/api/auth/logout", new RefreshRequest(auth.RefreshToken));
        var refresh = await PostAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_WithUnknownToken_Returns204()
    {
        var response = await PostAsync("/api/auth/logout", new RefreshRequest("unknown-token"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private Task<HttpResponseMessage> PostAsync<T>(string url, T body) => factory.CreateClient().PostAsJsonAsync(url, body);

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        Assert.Equal(status, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(title, problem!.Title);
    }
}