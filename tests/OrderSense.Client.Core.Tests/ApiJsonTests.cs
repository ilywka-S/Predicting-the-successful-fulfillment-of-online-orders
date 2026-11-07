using System.Net;
using System.Text;
using FluentAssertions;

namespace OrderSense.Client.Core.Tests;

public class ApiJsonTests
{
    [Fact]
    public async Task GetOrders_ReadsRiskLevelAndMissingPrediction()
    {
        var client = CreateClient("""
            {
              "items": [
                { "id": "a1", "purchasedAt": "2017-10-02T10:56:33+00:00", "status": "delivered", "customerState": "SP",
                  "itemsCount": 2, "totalValue": 38.71, "probability": 0.91, "riskLevel": "low" },
                { "id": "b2", "purchasedAt": "2017-10-03T08:00:00+00:00", "status": "shipped", "customerState": "RJ",
                  "itemsCount": 1, "totalValue": 120, "probability": null, "riskLevel": null }
              ],
              "page": 1, "pageSize": 20, "total": 2
            }
            """);

        var response = await client.GetOrdersAsync(null!, null, null, null!, RiskLevel.High, null!, OrderSort.DateAsc, 1, 20);

        var items = response.Items.ToList();
        items[0].RiskLevel.Value.Should().Be(RiskLevel.Low);
        items[0].Probability.Should().BeApproximately(0.91f, 0.0001f);
        items[1].RiskLevel.Should().BeNull();
        items[1].Probability.Should().BeNull();
    }

    [Fact]
    public async Task GetOrders_SendsEnumsInApiFormat()
    {
        var handler = new JsonHandler("""{ "items": [], "page": 1, "pageSize": 20, "total": 0 }""");
        var client = new Client(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") });

        await client.GetOrdersAsync(null!, null, null, null!, RiskLevel.High, null!, OrderSort.DateAsc, 2, 20);

        handler.LastRequestUri!.Query.Should().Contain("RiskLevel=high").And.Contain("Sort=dateAsc").And.Contain("Page=2");
    }

    [Fact]
    public async Task GetOrder_ReadsPredictionEnums()
    {
        var client = CreateClient("""
            {
              "id": "a1", "status": "delivered", "purchasedAt": "2017-10-02T10:56:33+00:00", "approvedAt": null,
              "estimatedDeliveryAt": "2017-10-18T00:00:00+00:00", "deliveredCustomerAt": null,
              "customer": { "city": "sao paulo", "state": "SP" },
              "items": [], "payments": [],
              "latestPrediction": {
                "probability": 0.42, "riskLevel": "high", "modelVersion": "v1", "createdAt": "2026-11-05T10:00:00+00:00",
                "factors": [ { "feature": "distance_km", "label": "Відстань", "value": 2300, "impact": "increases" } ]
              }
            }
            """);

        var order = await client.GetOrderAsync("a1");

        order.LatestPrediction.RiskLevel.Should().Be(RiskLevel.High);
        order.LatestPrediction.Factors.Single().Impact.Should().Be(FactorImpact.Increases);
    }

    private static Client CreateClient(string json) =>
        new(new HttpClient(new JsonHandler(json)) { BaseAddress = new Uri("https://api.example.test") });

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
