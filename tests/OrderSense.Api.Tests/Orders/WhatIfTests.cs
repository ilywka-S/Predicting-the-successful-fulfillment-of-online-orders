using System.Net;
using System.Net.Http.Json;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;
namespace OrderSense.Api.Tests.Orders;
[Collection(ApiCollection.Name)]
public class WhatIfTests(ApiFactory factory)
{
    private static readonly DateTime MondayMorning = new DateTime(2018, 3, 5, 10, 0, 0);
    private static readonly WhatIfRequest NoOverrides = new WhatIfRequest(null, null, null, null);
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string orderId, WhatIfRequest request)
    {
        return client.PostAsJsonAsync($"/api/orders/{orderId}/what-if", request);
    }
    [Fact]
    public async Task WhatIf_WithoutOverrides_ReturnsSamePredictionAndSavesNothing()
    {
        factory.Prediction.Reset();
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        HttpClient client = await factory.CreateManagerClientAsync();
        int callsBefore = factory.Prediction.Calls;
        HttpResponseMessage response = await PostAsync(client, order.Id, NoOverrides);
        WhatIfResultDto? result = await response.Content.ReadFromJsonAsync<WhatIfResultDto>(ApiJson.Options);
        List<PredictionHistoryItemDto>? history = await client.GetFromJsonAsync<List<PredictionHistoryItemDto>>(
            $"/api/orders/{order.Id}/predictions", ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(result.Before.Probability, result.After.Probability);
        Assert.Equal(result.Before.RiskLevel, result.After.RiskLevel);
        Assert.Equal(result.Before.ModelVersion, result.After.ModelVersion);
        Assert.Empty(result.Before.Factors);
        Assert.Equal(2, result.After.Factors.Count);
        Assert.Equal("PromisedDays", result.After.Factors[0].Feature);
        Assert.Equal(callsBefore + 1, factory.Prediction.Calls);
        Assert.NotNull(history);
        Assert.Empty(history);
    }
    [Fact]
    public async Task WhatIf_PromisedDaysOverride_ChangesFeaturesSentToModel()
    {
        factory.Prediction.Reset();
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await PostAsync(client, order.Id, new WhatIfRequest(25, null, null, null));
        OrderSense.Contracts.OrderFeatures features = factory.Prediction.LastFeatures!;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(25f, features.PromisedDays);
        Assert.Equal(1f, features.ItemsCount);
        Assert.Equal("credit_card", features.PaymentType);
    }
    [Fact]
    public async Task WhatIf_PaymentOverride_ChangesPaymentFeatures()
    {
        factory.Prediction.Reset();
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await PostAsync(client, order.Id, new WhatIfRequest(null, null, "boleto", 6));
        OrderSense.Contracts.OrderFeatures features = factory.Prediction.LastFeatures!;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("boleto", features.PaymentType);
        Assert.Equal(6f, features.Installments);
        Assert.Equal(10f, features.PromisedDays);
    }
    [Fact]
    public async Task WhatIf_SellerOverride_ChangesSellerStateAndDistance()
    {
        factory.Prediction.Reset();
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        string customerZip = data.Prefix[..5];
        string sellerZip = data.Prefix[3..];
        order.Customer.ZipPrefix = customerZip;
        Seller newSeller = new Seller { Id = data.Prefix + "newseller", ZipPrefix = sellerZip, City = "rio de janeiro", State = "RJ" };
        await factory.WithDbAsync(db =>
        {
            db.GeoZips.Add(new GeoZip { ZipPrefix = customerZip, Lat = -23.5505, Lng = -46.6333 });
            db.GeoZips.Add(new GeoZip { ZipPrefix = sellerZip, Lat = -22.9068, Lng = -43.1729 });
            db.Sellers.Add(newSeller);

            return db.SaveChangesAsync();
        });
        await factory.SeedAsync(order);
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await PostAsync(client, order.Id, new WhatIfRequest(null, newSeller.Id, null, null));
        OrderSense.Contracts.OrderFeatures features = factory.Prediction.LastFeatures!;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("RJ", features.SellerState);
        Assert.Equal("SP", features.CustomerState);
        Assert.InRange(features.DistanceKm, 350f, 365f);
    }
    [Fact]
    public async Task WhatIf_UnknownSeller_Returns400()
    {
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await PostAsync(client, order.Id, new WhatIfRequest(null, "does-not-exist", null, null));
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("sellerId", body);
    }
    [Theory]
    [InlineData(0, null, null)]
    [InlineData(366, null, null)]
    [InlineData(null, "cash", null)]
    [InlineData(null, null, 0)]
    [InlineData(null, null, 25)]
    public async Task WhatIf_InvalidOverrides_Returns400(int? promisedDays, string? paymentType, int? installments)
    {
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await PostAsync(client, order.Id, new WhatIfRequest(promisedDays, null, paymentType, installments));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task WhatIf_UnknownOrder_Returns404()
    {
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await PostAsync(client, "does-not-exist", new WhatIfRequest(20, null, null, null));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact]
    public async Task WhatIf_OrderWithoutItems_Returns422()
    {
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        order.Items = [];
        await factory.SeedAsync(order);
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await PostAsync(client, order.Id, NoOverrides);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
    [Fact]
    public async Task WhatIf_WhenServiceUnavailable_Returns503()
    {
        factory.Prediction.Reset();
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        HttpClient client = await factory.CreateManagerClientAsync();
        factory.Prediction.FailNextCall();
        HttpResponseMessage response = await PostAsync(client, order.Id, new WhatIfRequest(20, null, null, null));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
    [Fact]
    public async Task WhatIf_WhenExplainFails_ReturnsPredictionsWithoutFactors()
    {
        factory.Prediction.Reset();
        factory.Prediction.FailExplain = true;
        OrderTestData data = OrderTestData.Create();
        Order order = data.Order(MondayMorning);
        await factory.SeedAsync(order);
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await PostAsync(client, order.Id, new WhatIfRequest(20, null, null, null));
        WhatIfResultDto? result = await response.Content.ReadFromJsonAsync<WhatIfResultDto>(ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Empty(result.After.Factors);
        factory.Prediction.Reset();
    }
    [Fact]
    public async Task WhatIf_WithoutToken_Returns401()
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await PostAsync(client, "any", NoOverrides);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}