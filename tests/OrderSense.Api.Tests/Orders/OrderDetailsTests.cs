using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.Orders;

[Collection(ApiCollection.Name)]
public class OrderDetailsTests(ApiFactory factory)
{
    private static readonly DateTime Day = new(2018, 3, 10, 12, 0, 0);

    [Fact]
    public async Task GetById_ReturnsCustomerItemsAndPayments()
    {
        var data = OrderTestData.Create();
        var order = data.Order(Day, state: "RJ");
        await factory.SeedAsync(order);

        var details = await GetDetailsAsync(order.Id);

        Assert.Equal(order.Id, details.Id);
        Assert.Equal("RJ", details.Customer.State);
        var item = Assert.Single(details.Items);
        Assert.Equal("health_beauty", item.Category);
        Assert.Equal(100m, item.Price);
        Assert.Equal(15m, item.FreightValue);
        var payment = Assert.Single(details.Payments);
        Assert.Equal("credit_card", payment.Type);
        Assert.Null(details.LatestPrediction);
    }

    [Fact]
    public async Task GetById_ReturnsLatestPredictionWithTopFiveFactors()
    {
        var data = OrderTestData.Create();
        var order = data.Order(Day);
        data.AddPrediction(order, RiskLevel.Low, DateTimeOffset.UtcNow.AddHours(-1));
        data.AddPrediction(order, RiskLevel.High, DateTimeOffset.UtcNow,
            new RiskFactor { Feature = "DistanceKm", Value = 2300, Contribution = -0.30f },
            new RiskFactor { Feature = "PromisedDays", Value = 25, Contribution = 0.20f },
            new RiskFactor { Feature = "FreightShare", Value = 0.4f, Contribution = -0.10f },
            new RiskFactor { Feature = "TotalWeightG", Value = 9000, Contribution = -0.08f },
            new RiskFactor { Feature = "Installments", Value = 10, Contribution = -0.05f },
            new RiskFactor { Feature = "ItemsCount", Value = 1, Contribution = 0.01f });
        await factory.SeedAsync(order);

        var prediction = (await GetDetailsAsync(order.Id)).LatestPrediction;

        Assert.NotNull(prediction);
        Assert.Equal(RiskLevel.High, prediction.RiskLevel);
        Assert.Equal(5, prediction.Factors.Count);
        Assert.DoesNotContain(prediction.Factors, f => f.Feature == "ItemsCount");

        var distance = prediction.Factors[0];
        Assert.Equal("DistanceKm", distance.Feature);
        Assert.Equal("Відстань продавець -> клієнт", distance.Label);
        Assert.Equal(2300f, distance.Value);
        Assert.Equal(FactorImpact.Increases, distance.Impact);
        Assert.Equal(FactorImpact.Decreases, prediction.Factors[1].Impact);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404ProblemDetails()
    {
        var client = await factory.CreateManagerClientAsync();

        var response = await client.GetAsync("/api/orders/does-not-exist");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(404, problem!.Status);
    }

    [Fact]
    public async Task GetById_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/orders/any");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<OrderDetailsDto> GetDetailsAsync(string id)
    {
        var client = await factory.CreateManagerClientAsync();

        return (await client.GetFromJsonAsync<OrderDetailsDto>($"/api/orders/{id}", ApiJson.Options))!;
    }
}