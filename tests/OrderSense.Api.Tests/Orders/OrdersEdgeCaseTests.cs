using System.Net;
using System.Net.Http.Json;
using System.Text;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.Orders;

[Collection(ApiCollection.Name)]
public class OrdersEdgeCaseTests(ApiFactory factory)
{
    private static readonly DateTime Day = new(2018, 3, 10, 12, 0, 0);

    [Theory]
    [InlineData("to=9999-12-31", "To")]
    [InlineData("to=2100-01-01", "To")]
    [InlineData("from=0001-01-01", "From")]
    [InlineData("from=1999-12-31", "From")]
    [InlineData("from=2018-13-01", "From")]
    public async Task GetAll_DateOutsideSupportedRange_Returns400(string query, string field)
    {
        var client = await factory.CreateManagerClientAsync();

        var response = await client.GetAsync($"/api/orders?{query}");

        await ProblemAssert.ValidationAsync(response, field);
    }

    [Fact]
    public async Task GetAll_PageAfterLast_ReturnsEmptyItemsWithTotal()
    {
        var data = OrderTestData.Create();
        await factory.SeedAsync(data.Order(Day));
        var client = await factory.CreateManagerClientAsync();

        var page = await client.GetFromJsonAsync<PagedResponse<OrderListItemDto>>($"/api/orders?search={data.Prefix}&page=100000&pageSize=100", ApiJson.Options);

        Assert.Empty(page!.Items);
        Assert.Equal(1, page.Total);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    public async Task GetAll_SearchWithLikeWildcard_MatchesItLiterally(string wildcard)
    {
        var data = OrderTestData.Create();
        await factory.SeedAsync(data.Order(Day));
        var client = await factory.CreateManagerClientAsync();

        var page = await client.GetFromJsonAsync<PagedResponse<OrderListItemDto>>($"/api/orders?search={Uri.EscapeDataString(data.Prefix + wildcard)}", ApiJson.Options);

        Assert.Equal(0, page!.Total);
    }

    [Fact]
    public async Task GetById_TooLongId_Returns404()
    {
        var client = await factory.CreateManagerClientAsync();

        var response = await client.GetAsync($"/api/orders/{new string('a', 500)}");

        await ProblemAssert.StatusAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("{", "$")]
    [InlineData("{\"promisedDays\":\"5\"}", "request")]
    [InlineData("{\"installments\":1.5}", "request")]
    public async Task WhatIf_MalformedJson_Returns400(string json, string field)
    {
        var client = await factory.CreateManagerClientAsync();

        var response = await client.PostAsync("/api/orders/any/what-if", new StringContent(json, Encoding.UTF8, "application/json"));

        await ProblemAssert.ValidationAsync(response, field);
    }

    [Fact]
    public async Task WhatIf_WithoutJsonBody_Returns415()
    {
        var client = await factory.CreateManagerClientAsync();

        var response = await client.PostAsync("/api/orders/any/what-if", null);

        await ProblemAssert.StatusAsync(response, HttpStatusCode.UnsupportedMediaType);
    }
}