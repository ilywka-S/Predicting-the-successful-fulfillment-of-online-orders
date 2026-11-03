using OrderSense.Contracts;

namespace OrderSense.PredictionService;

public static class PredictValidator
{
    public static Dictionary<string, string[]> Validate(OrderFeatures f)
    {
        var e = new Dictionary<string, string[]>();

        if (!float.IsFinite(f.ItemsCount) || f.ItemsCount < 1)
            e[nameof(f.ItemsCount)] = ["Має бути числом ≥ 1."];
        if (!float.IsFinite(f.TotalPrice) || f.TotalPrice < 0)
            e[nameof(f.TotalPrice)] = ["Має бути числом ≥ 0."];
        if (!float.IsFinite(f.FreightShare) || f.FreightShare < 0 || f.FreightShare > 1)
            e[nameof(f.FreightShare)] = ["Має бути від 0 до 1."];

        // NaN дозволено: означає «невідомо»
        if (Bad(f.TotalWeightG, 0, float.MaxValue)) e[nameof(f.TotalWeightG)] = ["Має бути ≥ 0 або NaN."];
        if (Bad(f.DistanceKm, 0, 20000)) e[nameof(f.DistanceKm)] = ["Має бути від 0 до 20000 або NaN."];
        if (Bad(f.Installments, 0, 100)) e[nameof(f.Installments)] = ["Має бути від 0 до 100 або NaN."];
        if (Bad(f.PromisedDays, -365, 3650)) e[nameof(f.PromisedDays)] = ["Некоректне значення або NaN."];
        if (Bad(f.PurchaseDayOfWeek, 0, 6)) e[nameof(f.PurchaseDayOfWeek)] = ["Має бути від 0 до 6 або NaN."];

        return e;
    }

    private static bool Bad(float v, float min, float max) =>
        !float.IsNaN(v) && (!float.IsFinite(v) || v < min || v > max);
}