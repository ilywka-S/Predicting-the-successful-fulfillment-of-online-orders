using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Auth;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public class UsersEdgeCaseTests(ApiFactory factory)
{
    [Fact]
    public async Task Create_EmailLongerThan256_Returns400()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(new string('a', 300) + "@ordersense.test", ApiFactory.UserPassword, null, Roles.Manager));

        await ProblemAssert.ValidationAsync(response, "Email");
    }

    [Fact]
    public async Task Create_FullNameLongerThan100_Returns400()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(NewEmail(), ApiFactory.UserPassword, new string('a', 101), Roles.Manager));

        await ProblemAssert.ValidationAsync(response, "FullName");
    }

    [Fact]
    public async Task Create_ConcurrentRequestsWithSameEmail_CreateOneUser()
    {
        var client = await AdminClientAsync();
        var email = NewEmail();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.PostAsJsonAsync("/api/users", new CreateUserRequest(email, ApiFactory.UserPassword, null, Roles.Manager))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        Assert.Equal(1, await factory.WithDbAsync(db => db.Users.CountAsync(u => u.Email == email)));
    }

    private async Task<HttpClient> AdminClientAsync() => factory.CreateClient((await factory.LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword)).AccessToken);

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@ordersense.test";
}