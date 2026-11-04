using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderSense.Contracts;

namespace OrderSense.PredictionService.Tests;

public sealed class PredictEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    private readonly HttpClient _client;
        private static readonly string[] RiskLevels = ["low", "medium", "high"];

    public PredictEndpointTests(WebApplicationFactory<Program> factory) =>
        _client = factory.CreateClient();

    private static OrderFeatures Valid() => new()
    {
        ItemsCount = 1,
        TotalPrice = 120.5f,
        FreightShare = 0.15f,
        TotalWeightG = 800,
        PromisedDays = 20,
        DistanceKm = 450,
        CustomerState = "SP",
        SellerState = "RJ",
        MainCategory = "cama_mesa_banho",
        PaymentType = "credit_card",
        Installments = 1,
        PurchaseDayOfWeek = 2
    };

    [Fact]
    public async Task Predict_returns_probability_and_model_version()
    {
        var response = await _client.PostAsJsonAsync("/predict", Valid(), Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrderPrediction>(Json);
        Assert.NotNull(result);
        Assert.InRange(result.Probability, 0f, 1f);
        Assert.Equal("v1", result.ModelVersion);
        Assert.Contains(result.RiskLevel, RiskLevels);
    }

    [Fact]
    public async Task Predict_accepts_NaN_for_unknown_values()
    {
        var features = Valid();
        features.TotalWeightG = float.NaN;
        features.DistanceKm = float.NaN;
        features.Installments = float.NaN;

        var response = await _client.PostAsJsonAsync("/predict", features, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Predict_risk_level_matches_probability_of_problem()
    {
        var response = await _client.PostAsJsonAsync("/predict", Valid(), Json);
        var result = await response.Content.ReadFromJsonAsync<OrderPrediction>(Json);

        Assert.NotNull(result);
        var expected = new RiskThresholds().Level(1f - result.Probability);
        Assert.Equal(expected, result.RiskLevel);
    }

    [Fact]
    public async Task Predict_invalid_input_returns_400()
    {
        var features = Valid();
        features.ItemsCount = 0;
        features.FreightShare = 2;
        features.PurchaseDayOfWeek = 9;

        var response = await _client.PostAsJsonAsync("/predict", features, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ItemsCount", body);
        Assert.Contains("FreightShare", body);
        Assert.Contains("PurchaseDayOfWeek", body);
    }
        private static OrderFeatures Risky()
    {
        var f = Valid();
        f.PromisedDays = 8;
        f.DistanceKm = 2400;
        f.CustomerState = "AM";
        f.SellerState = "SP";
        return f;
    }

    [Fact]
    public async Task Explain_returns_top_factors_sorted_by_impact()
    {
        var response = await _client.PostAsJsonAsync("/explain", Risky(), Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrderExplanation>(Json);
        Assert.NotNull(result);
        Assert.Equal("v1", result.ModelVersion);
        Assert.InRange(result.Factors.Count, 1, 5);

        var impacts = result.Factors.Select(f => Math.Abs(f.Contribution)).ToList();
        Assert.Equal(impacts.OrderByDescending(x => x), impacts);
    }

    [Fact]
    public async Task Explain_short_promise_raises_risk()
    {
        var response = await _client.PostAsJsonAsync("/explain", Risky(), Json);
        var result = await response.Content.ReadFromJsonAsync<OrderExplanation>(Json);

        Assert.NotNull(result);
        var factor = Assert.Single(result.Factors, f => f.Feature == nameof(OrderFeatures.PromisedDays));
        Assert.True(factor.Contribution > 0);
    }

    [Fact]
    public async Task Explain_invalid_input_returns_400()
    {
        var features = Valid();
        features.ItemsCount = 0;

        var response = await _client.PostAsJsonAsync("/explain", features, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}