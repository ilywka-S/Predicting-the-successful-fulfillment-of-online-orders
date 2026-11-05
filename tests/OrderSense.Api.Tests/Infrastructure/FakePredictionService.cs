using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderSense.Contracts;

namespace OrderSense.Api.Tests.Infrastructure;

public sealed class FakePredictionService : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private bool _failNextCall;

    public OrderPrediction Result { get; set; } = new() { Probability = 0.9f, RiskLevel = "low", ModelVersion = "v-test" };

    public OrderFeatures? LastFeatures { get; private set; }

    public string? LastPath { get; private set; }

    public int Calls { get; private set; }

    public void FailNextCall() => _failNextCall = true;

    public void Reset()
    {
        _failNextCall = false;
        Result = new OrderPrediction { Probability = 0.9f, RiskLevel = "low", ModelVersion = "v-test" };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastPath = request.RequestUri?.AbsolutePath;

        if (_failNextCall)
        {
            _failNextCall = false;

            throw new HttpRequestException("Prediction service is down");
        }

        LastFeatures = await request.Content!.ReadFromJsonAsync<OrderFeatures>(Json, cancellationToken);

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Result, options: Json) };
    }
}