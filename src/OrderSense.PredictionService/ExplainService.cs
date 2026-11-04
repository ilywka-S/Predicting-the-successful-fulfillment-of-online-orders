using Microsoft.Extensions.ML;
using OrderSense.Contracts;

namespace OrderSense.PredictionService;

public sealed class ExplainService(PredictionEnginePool<OrderFeatures, ModelOutput> pool, ModelInfo model)
{
    private static readonly (string Name, Action<OrderFeatures> MakeUnknown)[] Variants =
    [
        (nameof(OrderFeatures.ItemsCount), x => x.ItemsCount = float.NaN),
        (nameof(OrderFeatures.TotalPrice), x => x.TotalPrice = float.NaN),
        (nameof(OrderFeatures.FreightShare), x => x.FreightShare = float.NaN),
        (nameof(OrderFeatures.TotalWeightG), x => x.TotalWeightG = float.NaN),
        (nameof(OrderFeatures.PromisedDays), x => x.PromisedDays = float.NaN),
        (nameof(OrderFeatures.DistanceKm), x => x.DistanceKm = float.NaN),
        (nameof(OrderFeatures.Installments), x => x.Installments = float.NaN),
        (nameof(OrderFeatures.PurchaseDayOfWeek), x => x.PurchaseDayOfWeek = float.NaN),
        (nameof(OrderFeatures.CustomerState), x => x.CustomerState = ""),
        (nameof(OrderFeatures.SellerState), x => x.SellerState = ""),
        (nameof(OrderFeatures.MainCategory), x => x.MainCategory = ""),
        (nameof(OrderFeatures.PaymentType), x => x.PaymentType = ""),
    ];

    public List<FeatureContribution> Explain(OrderFeatures features, int top = 5)
    {
        var actual = pool.Predict(model.Version, features).Probability;

        return Variants
            .Select(v =>
            {
                var copy = Copy(features);
                v.MakeUnknown(copy);
                var withoutFeature = pool.Predict(model.Version, copy).Probability;
                return new FeatureContribution { Feature = v.Name, Contribution = actual - withoutFeature };
            })
            .Where(c => c.Contribution != 0)
            .OrderByDescending(c => Math.Abs(c.Contribution))
            .Take(top)
            .ToList();
    }

    private static OrderFeatures Copy(OrderFeatures f) => new()
    {
        ItemsCount = f.ItemsCount,
        TotalPrice = f.TotalPrice,
        FreightShare = f.FreightShare,
        TotalWeightG = f.TotalWeightG,
        PromisedDays = f.PromisedDays,
        DistanceKm = f.DistanceKm,
        CustomerState = f.CustomerState,
        SellerState = f.SellerState,
        MainCategory = f.MainCategory,
        PaymentType = f.PaymentType,
        Installments = f.Installments,
        PurchaseDayOfWeek = f.PurchaseDayOfWeek
    };
}