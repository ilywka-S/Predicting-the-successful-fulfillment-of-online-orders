using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Contracts;
namespace OrderSense.Api.Predictions;
public sealed class BatchPredictor(AppDbContext db, HttpClient http, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };
    private readonly Dictionary<string, int> _modelVersionIds = new Dictionary<string, int>();
    public async Task<BatchRunResult> RunAsync(int batchSize, int maxOrders, CancellationToken ct, Func<BatchRunResult, CancellationToken, Task>? onBatchDone = null)
    {
        Dictionary<string, (double Lat, double Lng)> coordinates = await LoadCoordinatesAsync(ct);
        BatchRunResult result = new BatchRunResult();
        List<string> skippedIds = new List<string>();
        while (result.Predicted + result.Skipped < maxOrders)
        {
            int take = Math.Min(batchSize, maxOrders - result.Predicted - result.Skipped);
            List<string> ids = await db.Orders
                .AsNoTracking()
                .Where(o => o.Items.Any() && !o.Predictions.Any() && !skippedIds.Contains(o.Id))
                .OrderBy(o => o.Id)
                .Select(o => o.Id)
                .Take(take)
                .ToListAsync(ct);

            if (ids.Count == 0)
            {
                break;
            }
            List<BatchOrder> orders = await LoadOrdersAsync(ids, ct);
            MarkMissingAsSkipped(ids, orders, skippedIds, result);
            List<OrderFeatures> features = new List<OrderFeatures>();
            foreach (BatchOrder order in orders)
            {
                features.Add(BuildFeatures(order, coordinates));
            }

            List<OrderPrediction?> predictions = await PredictAsync(features, ct);
            for (int i = 0; i < orders.Count; i++)
            {
                OrderPrediction? prediction = predictions[i];
                if (prediction == null || !TryReadRisk(prediction, out RiskLevel risk))
                {
                    skippedIds.Add(orders[i].Id);
                    result.Skipped++;

                    continue;
                }
                int modelVersionId = await GetModelVersionIdAsync(prediction.ModelVersion, ct);
                db.Predictions.Add(new Prediction
                {
                    OrderId = orders[i].Id,
                    ModelVersionId = modelVersionId,
                    Probability = prediction.Probability,
                    RiskLevel = risk,
                    CreatedAt = _clock.GetUtcNow(),
                });
                result.Predicted++;
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            result.Batches++;
            if (onBatchDone != null)
            {
                await onBatchDone(result, ct);
            }
        }
        return result;
    }
    private async Task<Dictionary<string, (double Lat, double Lng)>> LoadCoordinatesAsync(CancellationToken ct)
    {
        List<GeoZip> zips = await db.GeoZips.AsNoTracking().ToListAsync(ct);
        Dictionary<string, (double Lat, double Lng)> result = new Dictionary<string, (double Lat, double Lng)>();
        foreach (GeoZip zip in zips)
        {
            result[zip.ZipPrefix] = (zip.Lat, zip.Lng);
        }
        return result;
    }
    private async Task<List<BatchOrder>> LoadOrdersAsync(List<string> ids, CancellationToken ct)
    {
        return await db.Orders
            .AsNoTracking()
            .AsSingleQuery()
            .Where(o => ids.Contains(o.Id))
            .Select(o => new BatchOrder
            {
                Id = o.Id,
                PurchasedAt = o.PurchasedAt,
                EstimatedDeliveryAt = o.EstimatedDeliveryAt,
                CustomerState = o.Customer.State,
                CustomerZip = o.Customer.ZipPrefix,
                Items = o.Items
                    .OrderBy(i => i.ItemNo)
                    .Select(i => new BatchItem
                    {
                        Price = i.Price,
                        FreightValue = i.FreightValue,
                        Category = i.Product.Category,
                        WeightG = i.Product.WeightG,
                        SellerId = i.SellerId,
                        SellerState = i.Seller.State,
                        SellerZip = i.Seller.ZipPrefix,
                    })
                    .ToList(),
                Payments = o.Payments
                    .OrderBy(p => p.Sequential)
                    .Select(p => new BatchPayment
                    {
                        Type = p.Type,
                        Installments = p.Installments,
                        Value = p.Value,
                    })
                    .ToList(),
            })
            .ToListAsync(ct);
    }
    private static void MarkMissingAsSkipped(List<string> ids, List<BatchOrder> orders, List<string> skippedIds, BatchRunResult result)
    {
        HashSet<string> loaded = new HashSet<string>();
        foreach (BatchOrder order in orders)
        {
            loaded.Add(order.Id);
        }
        foreach (string id in ids)
        {
            if (!loaded.Contains(id))
            {
                skippedIds.Add(id);
                result.Skipped++;
            }
        }
    }
    private static OrderFeatures BuildFeatures(BatchOrder order, Dictionary<string, (double Lat, double Lng)> coordinates)
    {
        List<OrderItemInput> itemInputs = new List<OrderItemInput>();
        foreach (BatchItem item in order.Items)
        {
            itemInputs.Add(new OrderItemInput((float)item.Price, (float)item.FreightValue, item.Category, item.WeightG));
        }
        List<OrderPaymentInput> paymentInputs = new List<OrderPaymentInput>();
        foreach (BatchPayment payment in order.Payments)
        {
            paymentInputs.Add(new OrderPaymentInput(payment.Type, payment.Installments, (float)payment.Value));
        }
        BatchItem? mainSeller = FindMainSellerItem(order.Items);
        string sellerState = "";
        (double Lat, double Lng)? sellerCoordinates = null;
        if (mainSeller != null)
        {
            sellerState = mainSeller.SellerState;
            sellerCoordinates = FindCoordinates(coordinates, mainSeller.SellerZip);
        }
        (double Lat, double Lng)? customerCoordinates = FindCoordinates(coordinates, order.CustomerZip);
        return OrderFeatureBuilder.Build(
            itemInputs,
            paymentInputs,
            order.CustomerState,
            sellerState,
            customerCoordinates,
            sellerCoordinates,
            order.PurchasedAt,
            order.EstimatedDeliveryAt);
    }
    private static BatchItem? FindMainSellerItem(List<BatchItem> items)
    {
        Dictionary<string, decimal> sums = new Dictionary<string, decimal>();
        List<BatchItem> firstItems = new List<BatchItem>();
        foreach (BatchItem item in items)
        {
            if (!sums.ContainsKey(item.SellerId))
            {
                sums[item.SellerId] = 0m;
                firstItems.Add(item);
            }
            sums[item.SellerId] += item.Price;
        }
        BatchItem? best = null;
        decimal bestSum = decimal.MinValue;
        foreach (BatchItem first in firstItems)
        {
            decimal sum = sums[first.SellerId];
            if (sum > bestSum)
            {
                best = first;
                bestSum = sum;
            }
        }
        return best;
    }
    private static (double Lat, double Lng)? FindCoordinates(Dictionary<string, (double Lat, double Lng)> coordinates, string zip)
    {
        if (coordinates.TryGetValue(zip, out (double Lat, double Lng) value))
        {
            return value;
        }
        return null;
    }
    private async Task<List<OrderPrediction?>> PredictAsync(List<OrderFeatures> features, CancellationToken ct)
    {
        List<OrderPrediction?> result = new List<OrderPrediction?>();
        using (HttpResponseMessage response = await http.PostAsJsonAsync("/predict/batch", features, Json, ct))
        {
            if (response.StatusCode != HttpStatusCode.BadRequest)
            {
                response.EnsureSuccessStatusCode();
                List<OrderPrediction>? predictions = await response.Content.ReadFromJsonAsync<List<OrderPrediction>>(Json, ct);
                if (predictions == null || predictions.Count != features.Count)
                {
                    throw new PredictionServiceUnavailableException("Сервіс прогнозування повернув неправильну кількість прогнозів");
                }
                foreach (OrderPrediction prediction in predictions)
                {
                    result.Add(prediction);
                }
                return result;
            }
        }
        foreach (OrderFeatures item in features)
        {
            using HttpResponseMessage single = await http.PostAsJsonAsync("/predict", item, Json, ct);
            if (!single.IsSuccessStatusCode)
            {
                result.Add(null);
                continue;
            }
            result.Add(await single.Content.ReadFromJsonAsync<OrderPrediction>(Json, ct));
        }
        return result;
    }
    private static bool TryReadRisk(OrderPrediction prediction, out RiskLevel risk)
    {
        risk = RiskLevel.Low;
        if (!Enum.TryParse(prediction.RiskLevel, true, out RiskLevel parsed) || !Enum.IsDefined(parsed))
        {
            return false;
        }
        if (!float.IsFinite(prediction.Probability) || prediction.Probability < 0 || prediction.Probability > 1)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(prediction.ModelVersion))
        {
            return false;
        }
        risk = parsed;
        return true;
    }
    private async Task<int> GetModelVersionIdAsync(string version, CancellationToken ct)
    {
        if (_modelVersionIds.TryGetValue(version, out int cached))
        {
            return cached;
        }
        ModelVersion? model = await db.ModelVersions.AsNoTracking().SingleOrDefaultAsync(m => m.Version == version, ct);
        if (model == null)
        {
            DateTimeOffset now = _clock.GetUtcNow();
            await db.Database.ExecuteSqlAsync($"INSERT INTO model_versions (version, trained_at, is_active) VALUES ({version}, {now}, TRUE) ON CONFLICT (version) DO NOTHING", ct);
            model = await db.ModelVersions.AsNoTracking().SingleAsync(m => m.Version == version, ct);
        }
        _modelVersionIds[version] = model.Id;
        return model.Id;
    }
}