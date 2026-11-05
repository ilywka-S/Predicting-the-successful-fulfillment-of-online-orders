using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Hubs;

namespace OrderSense.Api.Predictions;

public class OrderPredictor(AppDbContext db, OrderFeatureLoader featureLoader, PredictionClient client, IHubContext<OrdersHub, IOrdersClient> hub, TimeProvider timeProvider)
{
    public async Task<PredictionDto?> PredictAsync(string orderId, CancellationToken ct)
    {
        var features = await featureLoader.LoadAsync(orderId, ct);

        if (features is null)
        {
            return null;
        }

        if (features.ItemsCount < 1)
        {
            throw new OrderNotPredictableException($"Order '{orderId}' has no items to predict");
        }

        var result = await client.PredictAsync(features, ct);

        if (!Enum.TryParse<RiskLevel>(result.RiskLevel, ignoreCase: true, out var riskLevel) || !Enum.IsDefined(riskLevel) || !float.IsFinite(result.Probability) || result.Probability is < 0 or > 1 || string.IsNullOrWhiteSpace(result.ModelVersion))
        {
            throw new PredictionServiceUnavailableException("Prediction service returned an invalid result");
        }

        var model = await GetModelVersionAsync(result.ModelVersion, ct);

        var prediction = new Prediction
        {
            OrderId = orderId,
            ModelVersion = model,
            Probability = result.Probability,
            RiskLevel = riskLevel,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        db.Predictions.Add(prediction);
        await db.SaveChangesAsync(ct);

        var dto = new PredictionDto(prediction.Probability, prediction.RiskLevel, model.Version, prediction.CreatedAt, []);
        await hub.Clients.All.PredictionUpdated(orderId, dto);

        return dto;
    }

    private async Task<ModelVersion> GetModelVersionAsync(string version, CancellationToken ct)
    {
        var model = await db.ModelVersions.SingleOrDefaultAsync(m => m.Version == version, ct);

        if (model is not null)
        {
            return model;
        }

        await db.Database.ExecuteSqlAsync($"INSERT INTO model_versions (version, trained_at, is_active) VALUES ({version}, {timeProvider.GetUtcNow()}, TRUE) ON CONFLICT (version) DO NOTHING", ct);

        return await db.ModelVersions.SingleAsync(m => m.Version == version, ct);
    }
}

public class OrderNotPredictableException(string message) : Exception(message);