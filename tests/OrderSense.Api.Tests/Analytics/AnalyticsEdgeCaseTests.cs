using System.Net.Http.Json;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;
using OrderSense.Api.Tests.Orders;

namespace OrderSense.Api.Tests.Analytics;

[Collection(ApiCollection.Name)]
public class AnalyticsEdgeCaseTests(ApiFactory factory)
{
    [Theory]
    [InlineData("risk-by-day", "to=9999-12-31", "To")]
    [InlineData("risk-by-category", "from=0001-01-01", "From")]
    [InlineData("risk-by-state", "to=2100-01-01", "To")]
    [InlineData("risk-by-seller", "from=abc", "From")]
    public async Task Analytics_InvalidDate_Returns400(string endpoint, string query, string field)
    {
        var client = await factory.CreateManagerClientAsync();

        var response = await client.GetAsync($"/api/analytics/{endpoint}?{query}");

        await ProblemAssert.ValidationAsync(response, field);
    }

    [Theory]
    [InlineData("risk-by-day")]
    [InlineData("risk-by-category")]
    [InlineData("risk-by-state")]
    [InlineData("risk-by-seller")]
    public async Task Analytics_PeriodWithoutOrders_ReturnsEmptyList(string endpoint)
    {
        var client = await factory.CreateManagerClientAsync();

        var points = await client.GetFromJsonAsync<List<AnalyticsPointDto>>($"/api/analytics/{endpoint}?from=2000-01-01&to=2000-01-31", ApiJson.Options);

        Assert.Empty(points!);
    }
}