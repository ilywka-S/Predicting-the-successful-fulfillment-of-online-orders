using System.Net;
using System.Net.Http.Json;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;
using OrderSense.Api.Tests.Orders;
namespace OrderSense.Api.Tests.Models;
[Collection(ApiCollection.Name)]
public class ModelApiTests(ApiFactory factory)
{
    [Fact]
    public async Task GetModel_ReturnsDetailsFromPredictionService()
    {
        factory.Prediction.Reset();
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await client.GetAsync("/api/model");
        ModelInfoDto? info = await response.Content.ReadFromJsonAsync<ModelInfoDto>(ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/model", factory.Prediction.LastPath);
        Assert.NotNull(info);
        Assert.Equal("v-test", info.Version);
        Assert.Equal(0.61f, info.RocAuc);
        Assert.Equal(0.07f, info.PrAuc);
        Assert.Equal(0.06f, info.F1);
        Assert.Equal(0.04f, info.BaselinePrAuc);
        Assert.Equal(1000, info.TrainRows);
        Assert.Equal(200, info.TestRows);
        Assert.Equal(0.12f, info.RiskMediumFrom);
        Assert.Equal(0.20f, info.RiskHighFrom);
        Assert.Equal(2, info.FeatureImportances.Count);
        Assert.Equal("PromisedDays", info.FeatureImportances[0].Feature);
    }
    [Fact]
    public async Task GetModel_WhenDescriptionIsMissing_Returns404()
    {
        factory.Prediction.Reset();
        factory.Prediction.Model = null;
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await client.GetAsync("/api/model");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        factory.Prediction.Reset();
    }
    [Fact]
    public async Task GetModel_WhenServiceUnavailable_Returns503()
    {
        factory.Prediction.Reset();
        factory.Prediction.FailNextCall();
        HttpClient client = await factory.CreateManagerClientAsync();
        HttpResponseMessage response = await client.GetAsync("/api/model");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
    [Fact]
    public async Task GetModel_WithoutToken_Returns401()
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/model");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}