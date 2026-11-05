using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;
using OrderSense.Contracts;

namespace OrderSense.Api.Tests.Orders;

[Collection(ApiCollection.Name)]
public class OrderPredictionTests(ApiFactory factory)
{
    private static readonly DateTime MondayMorning = new(2018, 3, 5, 10, 0, 0);

    [Fact]
    public async Task Predict_ExistingOrder_SavesAndReturnsPrediction()
    {
        var data = OrderTestData.Create();
        var order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        factory.Prediction.Result = new OrderPrediction { Probability = 0.83f, RiskLevel = "medium", ModelVersion = $"v-{data.Prefix}" };
        var client = await factory.CreateManagerClientAsync();

        var response = await client.PostAsync($"/api/orders/{order.Id}/predict", null);
        var prediction = await response.Content.ReadFromJsonAsync<PredictionDto>(ApiJson.Options);
        var details = await client.GetFromJsonAsync<OrderDetailsDto>($"/api/orders/{order.Id}", ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/predict", factory.Prediction.LastPath);
        Assert.Equal(0.83f, prediction!.Probability);
        Assert.Equal(RiskLevel.Medium, prediction.RiskLevel);
        Assert.Equal($"v-{data.Prefix}", prediction.ModelVersion);
        Assert.Empty(prediction.Factors);
        Assert.Equal(RiskLevel.Medium, details!.LatestPrediction!.RiskLevel);
        Assert.Equal($"v-{data.Prefix}", details.LatestPrediction.ModelVersion);
    }

    [Fact]
    public async Task Predict_SendsFeaturesBuiltFromOrder()
    {
        factory.Prediction.Reset();
        var data = OrderTestData.Create();
        var order = data.Order(MondayMorning);
        var customerZip = data.Prefix[..5];
        var sellerZip = data.Prefix[3..];
        order.Customer.ZipPrefix = customerZip;
        order.Items[0].Seller.ZipPrefix = sellerZip;
        await factory.WithDbAsync(db =>
        {
            db.GeoZips.AddRange(
                new GeoZip { ZipPrefix = customerZip, Lat = -23.5505, Lng = -46.6333 },
                new GeoZip { ZipPrefix = sellerZip, Lat = -22.9068, Lng = -43.1729 });

            return db.SaveChangesAsync();
        });
        await factory.SeedAsync(order);
        var client = await factory.CreateManagerClientAsync();

        await client.PostAsync($"/api/orders/{order.Id}/predict", null);
        var features = factory.Prediction.LastFeatures!;

        Assert.Equal(1, features.ItemsCount);
        Assert.Equal(100f, features.TotalPrice);
        Assert.Equal(15f / 115f, features.FreightShare, 0.0001f);
        Assert.Equal(500f, features.TotalWeightG);
        Assert.Equal(10f, features.PromisedDays);
        Assert.InRange(features.DistanceKm, 350f, 365f);
        Assert.Equal("SP", features.CustomerState);
        Assert.Equal("SP", features.SellerState);
        Assert.Equal("beleza_saude", features.MainCategory);
        Assert.Equal("credit_card", features.PaymentType);
        Assert.Equal(1f, features.Installments);
        Assert.Equal((float)DayOfWeek.Monday, features.PurchaseDayOfWeek);
    }

    [Fact]
    public async Task Predict_WithoutCoordinates_SendsUnknownDistance()
    {
        factory.Prediction.Reset();
        var order = OrderTestData.Create().Order(MondayMorning);
        order.Customer.ZipPrefix = "zzzzz";
        await factory.SeedAsync(order);
        var client = await factory.CreateManagerClientAsync();

        var response = await client.PostAsync($"/api/orders/{order.Id}/predict", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(float.IsNaN(factory.Prediction.LastFeatures!.DistanceKm));
    }

    [Fact]
    public async Task Predict_TwiceWithSameModel_ReusesModelVersionAndKeepsHistory()
    {
        var data = OrderTestData.Create();
        var order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        var version = $"v-{data.Prefix}";
        factory.Prediction.Result = new OrderPrediction { Probability = 0.95f, RiskLevel = "low", ModelVersion = version };
        var client = await factory.CreateManagerClientAsync();

        await client.PostAsync($"/api/orders/{order.Id}/predict", null);
        factory.Prediction.Result = new OrderPrediction { Probability = 0.6f, RiskLevel = "high", ModelVersion = version };
        await client.PostAsync($"/api/orders/{order.Id}/predict", null);

        var models = await factory.WithDbAsync(db => db.ModelVersions.CountAsync(m => m.Version == version));
        var predictions = await factory.WithDbAsync(db => db.Predictions.CountAsync(p => p.OrderId == order.Id));
        var details = await client.GetFromJsonAsync<OrderDetailsDto>($"/api/orders/{order.Id}", ApiJson.Options);

        Assert.Equal(1, models);
        Assert.Equal(2, predictions);
        Assert.Equal(RiskLevel.High, details!.LatestPrediction!.RiskLevel);
    }

    [Fact]
    public async Task Predict_UnknownOrder_Returns404WithoutCallingService()
    {
        var client = await factory.CreateManagerClientAsync();
        var callsBefore = factory.Prediction.Calls;

        var response = await client.PostAsync("/api/orders/unknown-order/predict", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(callsBefore, factory.Prediction.Calls);
    }

    [Fact]
    public async Task Predict_OrderWithoutItems_Returns422()
    {
        var order = OrderTestData.Create().Order(MondayMorning);
        order.Items.Clear();
        await factory.SeedAsync(order);
        var client = await factory.CreateManagerClientAsync();

        var response = await client.PostAsync($"/api/orders/{order.Id}/predict", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Predict_WhenServiceUnavailable_Returns503AndSavesNothing()
    {
        var order = OrderTestData.Create().Order(MondayMorning);
        await factory.SeedAsync(order);
        var client = await factory.CreateManagerClientAsync();
        factory.Prediction.FailNextCall();

        var response = await client.PostAsync($"/api/orders/{order.Id}/predict", null);
        var predictions = await factory.WithDbAsync(db => db.Predictions.CountAsync(p => p.OrderId == order.Id));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, predictions);
    }

    [Theory]
    [InlineData(0.5f, "extreme", "v1")]
    [InlineData(1.5f, "low", "v1")]
    [InlineData(0.5f, "low", "")]
    public async Task Predict_WithInvalidServiceResult_Returns503(float probability, string riskLevel, string modelVersion)
    {
        var order = OrderTestData.Create().Order(MondayMorning);
        await factory.SeedAsync(order);
        factory.Prediction.Result = new OrderPrediction { Probability = probability, RiskLevel = riskLevel, ModelVersion = modelVersion };
        var client = await factory.CreateManagerClientAsync();

        var response = await client.PostAsync($"/api/orders/{order.Id}/predict", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Predict_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsync("/api/orders/any/predict", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}