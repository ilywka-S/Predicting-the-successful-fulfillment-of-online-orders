using System.Net;
using System.Net.Http.Json;
using System.Text;
using OrderSense.Api.Auth;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public class AuthEdgeCaseTests(ApiFactory factory)
{
    [Theory]
    [InlineData("{\"email\":", "request")]
    [InlineData("{}", "Email")]
    [InlineData("{\"email\":\"user@ordersense.test\"}", "Password")]
    public async Task Login_InvalidBody_Returns400(string json, string field)
    {
        var response = await factory.CreateClient().PostAsync("/api/auth/login", new StringContent(json, Encoding.UTF8, "application/json"));

        await ProblemAssert.ValidationAsync(response, field);
    }

    [Fact]
    public async Task Login_FormInsteadOfJson_Returns415()
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = ApiFactory.AdminEmail });

        var response = await factory.CreateClient().PostAsync("/api/auth/login", form);

        await ProblemAssert.StatusAsync(response, HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Login_VeryLongCredentials_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(new string('a', 1000) + "@ordersense.test", new string('a', 100_000)));

        await ProblemAssert.StatusAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_EmptyToken_Returns400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(""));

        await ProblemAssert.ValidationAsync(response, "RefreshToken");
    }

    [Fact]
    public async Task Refresh_ConcurrentRequestsWithSameToken_OnlyOneSucceeds()
    {
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(Roles.Manager));

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => factory.CreateClient().PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(auth.RefreshToken))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }

    [Fact]
    public async Task Me_WithMalformedToken_Returns401()
    {
        var response = await factory.CreateClient("not-a-jwt").GetAsync("/api/auth/me");

        await ProblemAssert.StatusAsync(response, HttpStatusCode.Unauthorized);
    }
}