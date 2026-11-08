using System.Net;
using System.Net.Http.Json;
using OrderSense.Api.Auth;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.Contracts;

[Collection(ApiCollection.Name)]
public class ContractStubTests(ApiFactory factory)
{
    [Fact]
    public async Task BatchJobs_AsManager_Returns403()
    {
        var client = await ClientAsync(Roles.Manager);

        var response = await client.GetAsync("/api/batch-jobs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/batch-jobs/template")]
    public async Task BatchJobs_AsAnalyst_Return501(string url)
    {
        var client = await ClientAsync(Roles.Analyst);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task CreateBatchJob_AsAnalyst_Returns501()
    {
        var client = await ClientAsync(Roles.Analyst);
        
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent("ItemsCount\n1\n"u8.ToArray()), "file", "orders.csv" },
        };

        var response = await client.PostAsync("/api/batch-jobs", content);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task WhatIf_ValidRequest_Returns501()
    {
        var client = await ClientAsync(Roles.Manager);

        var response = await client.PostAsJsonAsync("/api/orders/any/what-if", new WhatIfRequest(20, null, "boleto", 1));

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task WhatIf_InvalidRequest_Returns400()
    {
        var client = await ClientAsync(Roles.Manager);

        var response = await client.PostAsJsonAsync("/api/orders/any/what-if", new WhatIfRequest(0, null, "cash", 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_ContainsAllNewPaths()
    {
        var document = await factory.CreateClient().GetStringAsync("/openapi/v1.json");

        string[] paths =
        [
            "/api/orders/{id}/what-if", "/api/batch-jobs", "/api/batch-jobs/{id}",
            "/api/batch-jobs/{id}/results", "/api/batch-jobs/{id}/export", "/api/batch-jobs/template", "/api/model",
        ];

        Assert.All(paths, path => Assert.Contains($"\"{path}\"", document));
    }

    private async Task<HttpClient> ClientAsync(string role)
    {
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(role));

        return factory.CreateClient(auth.AccessToken);
    }
}