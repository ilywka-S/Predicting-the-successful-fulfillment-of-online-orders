using System.Net;
using System.Net.Http.Json;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;
using OrderSense.Contracts;
namespace OrderSense.Api.Tests.Orders;
[Collection(ApiCollection.Name)]
public class OrderFactorsTests(ApiFactory factory)
{
    private static readonly DateTime MondayMorning = new DateTime(2018, 3, 5, 10, 0, 0);
    [Fact]
    public async Task Predict_SavesFactorsFromExplainWithLabelsAndDisplayValues()
    {
        factory.Prediction.Reset();
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        factory.Prediction.Explanation = new OrderExplanation
        {
            ModelVersion = "v-test",
            Factors =
            [
                new FeatureContribution { Feature = "PromisedDays", Contribution = 0.30f },
                new FeatureContribution { Feature = "CustomerState", Contribution = -0.10f },
                new FeatureContribution { Feature = "ItemsCount", Contribution = 0.05f },
            ],
        };
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await client.PostAsync($"/api/orders/{order.Id}/predict", null);
        PredictionDto? prediction = await response.Content.ReadFromJsonAsync<PredictionDto>(ApiJson.Options);
        OrderDetailsDto? details = await client.GetFromJsonAsync<OrderDetailsDto>($"/api/orders/{order.Id}", ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(prediction);
        Assert.Equal(3, prediction.Factors.Count);
        Assert.Equal("PromisedDays", prediction.Factors[0].Feature);
        Assert.Equal("Обіцяний термін доставки", prediction.Factors[0].Label);
        Assert.Equal(10f, prediction.Factors[0].Value);
        Assert.Equal("10 дн.", prediction.Factors[0].DisplayValue);
        Assert.Equal(FactorImpact.Increases, prediction.Factors[0].Impact);
        Assert.Equal("CustomerState", prediction.Factors[1].Feature);
        Assert.Equal("SP", prediction.Factors[1].DisplayValue);
        Assert.Null(prediction.Factors[1].Value);
        Assert.Equal(FactorImpact.Decreases, prediction.Factors[1].Impact);
        Assert.NotNull(details);
        Assert.NotNull(details.LatestPrediction);
        Assert.Equal(3, details.LatestPrediction.Factors.Count);
        Assert.Equal("10 дн.", details.LatestPrediction.Factors[0].DisplayValue);
        Assert.Equal("SP", details.LatestPrediction.Factors[1].DisplayValue);
        Assert.Equal("1 шт.", details.LatestPrediction.Factors[2].DisplayValue);
    }
    [Fact]
    public async Task Predict_KeepsOnlyFiveLargestFactors()
    {
        factory.Prediction.Reset();
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        factory.Prediction.Explanation = new OrderExplanation
        {
            ModelVersion = "v-test",
            Factors =
            [
                new FeatureContribution { Feature = "ItemsCount", Contribution = 0.01f },
                new FeatureContribution { Feature = "TotalPrice", Contribution = -0.02f },
                new FeatureContribution { Feature = "FreightShare", Contribution = 0.30f },
                new FeatureContribution { Feature = "TotalWeightG", Contribution = -0.25f },
                new FeatureContribution { Feature = "PromisedDays", Contribution = 0.20f },
                new FeatureContribution { Feature = "DistanceKm", Contribution = 0.15f },
                new FeatureContribution { Feature = "Installments", Contribution = -0.10f },
            ],
        };
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await client.PostAsync($"/api/orders/{order.Id}/predict", null);
        PredictionDto? prediction = await response.Content.ReadFromJsonAsync<PredictionDto>(ApiJson.Options);
        Assert.NotNull(prediction);
        Assert.Equal(5, prediction.Factors.Count);
        Assert.Equal("FreightShare", prediction.Factors[0].Feature);
        Assert.Equal("TotalWeightG", prediction.Factors[1].Feature);
        Assert.Equal("Installments", prediction.Factors[4].Feature);
        Assert.DoesNotContain(prediction.Factors, f => f.Feature == "ItemsCount");
        Assert.DoesNotContain(prediction.Factors, f => f.Feature == "TotalPrice");
    }
    [Fact]
    public async Task Predict_WhenExplainFails_SavesPredictionWithoutFactors()
    {
        factory.Prediction.Reset();
        factory.Prediction.FailExplain = true;
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await client.PostAsync($"/api/orders/{order.Id}/predict", null);
        PredictionDto? prediction = await response.Content.ReadFromJsonAsync<PredictionDto>(ApiJson.Options);
        OrderDetailsDto? details = await client.GetFromJsonAsync<OrderDetailsDto>($"/api/orders/{order.Id}", ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(prediction);
        Assert.Empty(prediction.Factors);
        Assert.NotNull(details);
        Assert.NotNull(details.LatestPrediction);
        Assert.Empty(details.LatestPrediction.Factors);
        factory.Prediction.Reset();
    }
    [Fact]
    public async Task GetPredictions_ReturnsHistoryNewestFirst()
    {
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        data.AddPrediction(order, RiskLevel.Low, new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero));
        data.AddPrediction(order, RiskLevel.High, new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero));
        data.AddPrediction(order, RiskLevel.Medium, new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
        await factory.SeedAsync(order);
        HttpClient client = await factory.CreateManagerClientAsync();
        List<PredictionHistoryItemDto>? history = await client.GetFromJsonAsync<List<PredictionHistoryItemDto>>(
            $"/api/orders/{order.Id}/predictions", ApiJson.Options);
        Assert.NotNull(history);
        Assert.Equal(3, history.Count);
        Assert.Equal(RiskLevel.High, history[0].RiskLevel);
        Assert.Equal(RiskLevel.Medium, history[1].RiskLevel);
        Assert.Equal(RiskLevel.Low, history[2].RiskLevel);
        Assert.Equal($"test-{data.Prefix}", history[0].ModelVersion);
        Assert.Equal(0.4f, history[0].Probability);
    }
    [Fact]
    public async Task GetPredictions_OrderWithoutPredictions_ReturnsEmptyList()
    {
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        HttpClient client = await factory.CreateManagerClientAsync();
        List<PredictionHistoryItemDto>? history = await client.GetFromJsonAsync<List<PredictionHistoryItemDto>>(
            $"/api/orders/{order.Id}/predictions", ApiJson.Options);
        Assert.NotNull(history);
        Assert.Empty(history);
    }
    [Fact]
    public async Task GetPredictions_UnknownOrder_Returns404()
    {
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await client.GetAsync("/api/orders/does-not-exist/predictions");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact]
    public async Task GetPredictions_WithoutToken_Returns401()
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/orders/any/predictions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}