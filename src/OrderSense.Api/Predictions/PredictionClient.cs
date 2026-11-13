using System.Text.Json;
using System.Text.Json.Serialization;
using OrderSense.Contracts;
using Polly;
using System.Net;

namespace OrderSense.Api.Predictions;

public class PredictionClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };
    public async Task<OrderPrediction> PredictAsync(OrderFeatures features, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("/predict", features, Json, ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<OrderPrediction>(Json, ct) ?? throw new PredictionServiceUnavailableException("Prediction service returned an empty response");
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or ExecutionRejectedException || e is TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new PredictionServiceUnavailableException("Prediction service is unavailable", e);
        }
    }
    public async Task<OrderExplanation> ExplainAsync(OrderFeatures features, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await http.PostAsJsonAsync("/explain", features, Json, ct);
            response.EnsureSuccessStatusCode();
            OrderExplanation? explanation = await response.Content.ReadFromJsonAsync<OrderExplanation>(Json, ct);
            if (explanation == null)
            {
                throw new PredictionServiceUnavailableException("Prediction service returned an empty response");
            }
            return explanation;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or ExecutionRejectedException || e is TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new PredictionServiceUnavailableException("Prediction service is unavailable", e);
        }
    }
    public async Task<ModelDetails?> GetModelAsync(CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await http.GetAsync("/model", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            ModelDetails? details = await response.Content.ReadFromJsonAsync<ModelDetails>(Json, ct);
            if (details == null)
            {
                throw new PredictionServiceUnavailableException("Prediction service returned an empty response");
            }
            return details;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or ExecutionRejectedException || e is TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new PredictionServiceUnavailableException("Prediction service is unavailable", e);
        }
    }
    public async Task<IReadOnlyList<OrderPrediction>?> PredictBatchAsync(IReadOnlyList<OrderFeatures> features, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await http.PostAsJsonAsync("/predict/batch", features, Json, ct);
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();
            List<OrderPrediction>? result = await response.Content.ReadFromJsonAsync<List<OrderPrediction>>(Json, ct);
            if (result == null || result.Count != features.Count)
            {
                throw new PredictionServiceUnavailableException("Prediction service returned a wrong number of predictions");
            }
            return result;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or ExecutionRejectedException || e is TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new PredictionServiceUnavailableException("Prediction service is unavailable", e);
        }
    }
}

public class PredictionServiceUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);