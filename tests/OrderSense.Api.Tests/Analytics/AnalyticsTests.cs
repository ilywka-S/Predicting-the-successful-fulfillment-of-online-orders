using System.Net;
using System.Net.Http.Json;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;
using OrderSense.Api.Tests.Orders;
namespace OrderSense.Api.Tests.Analytics;
[Collection(ApiCollection.Name)]
public class AnalyticsTests(ApiFactory factory)
{
    private static AnalyticsPointDto Find(List<AnalyticsPointDto> points, string key)
    {
        foreach (AnalyticsPointDto point in points)
        {
            if (point.Key == key)
            {
                return point;
            }
        }
        throw new InvalidOperationException($"Point '{key}' not found");
    }
    [Fact]
    public async Task RiskByState_UsesLatestPredictionAndCountsOrdersWithoutOne()
    {
        OrderTestData data = OrderTestData.Create();
        DateTime day = new DateTime(2029, 3, 5, 10, 0, 0);
        DateTimeOffset earlier = new DateTimeOffset(2029, 3, 6, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset later = new DateTimeOffset(2029, 3, 6, 10, 0, 0, TimeSpan.Zero);
        Order changedMind = data.Order(day, state: "ZZ");
        data.AddPrediction(changedMind, RiskLevel.Low, earlier);
        data.AddPrediction(changedMind, RiskLevel.High, later);
        Order medium = data.Order(day, state: "ZZ");
        data.AddPrediction(medium, RiskLevel.Medium, earlier);
        Order withoutPrediction = data.Order(day, state: "ZZ");
        Order otherState = data.Order(day, state: "YY");
        data.AddPrediction(otherState, RiskLevel.Low, earlier);
        await factory.SeedAsync(changedMind, medium, withoutPrediction, otherState);
        HttpClient client = await factory.CreateManagerClientAsync();
        List<AnalyticsPointDto>? points = await client.GetFromJsonAsync<List<AnalyticsPointDto>>(
            "/api/analytics/risk-by-state?from=2029-03-05&to=2029-03-05", ApiJson.Options);
        Assert.NotNull(points);
        AnalyticsPointDto zz = Find(points, "ZZ");
        AnalyticsPointDto yy = Find(points, "YY");

        Assert.Equal(3, zz.Orders);
        Assert.Equal(2, zz.Predicted);
        Assert.Equal(0, zz.Low);
        Assert.Equal(1, zz.Medium);
        Assert.Equal(1, zz.High);
        Assert.Equal(1.0, zz.RiskyShare);
        Assert.Equal(1, yy.Orders);
        Assert.Equal(1, yy.Low);
        Assert.Equal(0.0, yy.RiskyShare);
    }
    [Fact]
    public async Task RiskByDay_GroupsByPurchaseDateAndOrdersByDate()
    {
        OrderTestData data = OrderTestData.Create();
        DateTimeOffset createdAt = new DateTimeOffset(2030, 3, 7, 9, 0, 0, TimeSpan.Zero);

        Order morning = data.Order(new DateTime(2030, 3, 5, 8, 0, 0), state: "ZZ");
        data.AddPrediction(morning, RiskLevel.Low, createdAt);

        Order evening = data.Order(new DateTime(2030, 3, 5, 20, 0, 0), state: "ZZ");
        data.AddPrediction(evening, RiskLevel.High, createdAt);

        Order nextDay = data.Order(new DateTime(2030, 3, 6, 12, 0, 0), state: "ZZ");
        data.AddPrediction(nextDay, RiskLevel.Medium, createdAt);

        await factory.SeedAsync(morning, evening, nextDay);
        HttpClient client = await factory.CreateManagerClientAsync();

        List<AnalyticsPointDto>? points = await client.GetFromJsonAsync<List<AnalyticsPointDto>>(
            "/api/analytics/risk-by-day?from=2030-03-05&to=2030-03-06", ApiJson.Options);

        Assert.NotNull(points);
        Assert.Equal(2, points.Count);
        Assert.Equal("2030-03-05", points[0].Key);
        Assert.Equal(2, points[0].Orders);
        Assert.Equal(1, points[0].Low);
        Assert.Equal(1, points[0].High);
        Assert.Equal(0.5, points[0].RiskyShare);
        Assert.Equal("2030-03-06", points[1].Key);
        Assert.Equal(1.0, points[1].RiskyShare);
    }
    [Fact]
    public async Task RiskByCategory_UsesMostExpensiveItemCategoryAndRespectsTop()
    {
        OrderTestData data = OrderTestData.Create();
        string categoryA = "cat-a-" + data.Prefix;
        string categoryB = "cat-b-" + data.Prefix;
        DateTimeOffset createdAt = new DateTimeOffset(2031, 3, 7, 9, 0, 0, TimeSpan.Zero);
        DateTime day = new DateTime(2031, 3, 5, 10, 0, 0);

        Order first = data.Order(day);
        first.Items[0].Product.CategoryEn = categoryA;
        data.AddPrediction(first, RiskLevel.High, createdAt);

        Order second = data.Order(day);
        second.Items[0].Product.CategoryEn = categoryA;
        data.AddPrediction(second, RiskLevel.Low, createdAt);

        Order third = data.Order(day);
        third.Items[0].Product.CategoryEn = categoryB;
        data.AddPrediction(third, RiskLevel.Low, createdAt);

        await factory.SeedAsync(first, second, third);
        HttpClient client = await factory.CreateManagerClientAsync();

        List<AnalyticsPointDto>? all = await client.GetFromJsonAsync<List<AnalyticsPointDto>>(
            "/api/analytics/risk-by-category?from=2031-03-05&to=2031-03-05", ApiJson.Options);
        List<AnalyticsPointDto>? top = await client.GetFromJsonAsync<List<AnalyticsPointDto>>(
            "/api/analytics/risk-by-category?from=2031-03-05&to=2031-03-05&top=1", ApiJson.Options);

        Assert.NotNull(all);
        Assert.NotNull(top);
        Assert.Equal(2, all.Count);
        Assert.Equal(2, Find(all, categoryA).Orders);
        Assert.Equal(0.5, Find(all, categoryA).RiskyShare);
        Assert.Equal(1, Find(all, categoryB).Orders);
        Assert.Single(top);
        Assert.Equal(categoryA, top[0].Key);
    }
    [Fact]
    public async Task RiskBySeller_GroupsOrdersOfTheSameSeller()
    {
        OrderTestData data = OrderTestData.Create();
        DateTimeOffset createdAt = new DateTimeOffset(2032, 3, 7, 9, 0, 0, TimeSpan.Zero);
        DateTime day = new DateTime(2032, 3, 5, 10, 0, 0);

        Order first = data.Order(day);
        data.AddPrediction(first, RiskLevel.High, createdAt);

        Order second = data.Order(day);
        second.Items[0].Seller = first.Items[0].Seller;
        second.Items[0].SellerId = first.Items[0].SellerId;
        data.AddPrediction(second, RiskLevel.Low, createdAt);

        Order other = data.Order(day);
        data.AddPrediction(other, RiskLevel.Low, createdAt);

        await factory.SeedAsync(first, second, other);
        HttpClient client = await factory.CreateManagerClientAsync();

        List<AnalyticsPointDto>? points = await client.GetFromJsonAsync<List<AnalyticsPointDto>>(
            "/api/analytics/risk-by-seller?from=2032-03-05&to=2032-03-05", ApiJson.Options);

        Assert.NotNull(points);
        Assert.Equal(2, points.Count);
        Assert.Equal(first.Items[0].SellerId, points[0].Key);
        Assert.Equal(2, points[0].Orders);
        Assert.Equal(0.5, points[0].RiskyShare);
    }
    [Fact]
    public async Task Analytics_PeriodBoundsAreInclusiveByDate()
    {
        OrderTestData data = OrderTestData.Create();
        DateTimeOffset createdAt = new DateTimeOffset(2033, 5, 10, 9, 0, 0, TimeSpan.Zero);

        Order beforePeriod = data.Order(new DateTime(2033, 5, 4, 23, 59, 0), state: "XX");
        Order startOfPeriod = data.Order(new DateTime(2033, 5, 5, 0, 0, 0), state: "XX");
        Order endOfPeriod = data.Order(new DateTime(2033, 5, 5, 23, 30, 0), state: "XX");
        Order afterPeriod = data.Order(new DateTime(2033, 5, 6, 0, 10, 0), state: "XX");

        data.AddPrediction(startOfPeriod, RiskLevel.Low, createdAt);
        data.AddPrediction(endOfPeriod, RiskLevel.Low, createdAt);

        await factory.SeedAsync(beforePeriod, startOfPeriod, endOfPeriod, afterPeriod);
        HttpClient client = await factory.CreateManagerClientAsync();

        List<AnalyticsPointDto>? points = await client.GetFromJsonAsync<List<AnalyticsPointDto>>(
            "/api/analytics/risk-by-state?from=2033-05-05&to=2033-05-05", ApiJson.Options);

        Assert.NotNull(points);
        Assert.Equal(2, Find(points, "XX").Orders);
    }
    [Theory]
    [InlineData("/api/analytics/risk-by-day?from=2030-02-01&to=2030-01-01")]
    [InlineData("/api/analytics/risk-by-category?top=0")]
    [InlineData("/api/analytics/risk-by-seller?top=51")]
    public async Task Analytics_InvalidQuery_Returns400(string url)
    {
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task Analytics_WithoutToken_Returns401()
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/analytics/risk-by-state");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}