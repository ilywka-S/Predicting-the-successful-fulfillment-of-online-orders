using OrderSense.Contracts;
namespace OrderSense.Api.Predictions;
public static class FactorValues
{
    public static float? Numeric(OrderFeatures features, string feature)
    {
        float value;
        switch (feature)
        {
            case "ItemsCount":
                value = features.ItemsCount;
                break;
            case "TotalPrice":
                value = features.TotalPrice;
                break;
            case "FreightShare":
                value = features.FreightShare;
                break;
            case "TotalWeightG":
                value = features.TotalWeightG;
                break;
            case "PromisedDays":
                value = features.PromisedDays;
                break;
            case "DistanceKm":
                value = features.DistanceKm;
                break;
            case "Installments":
                value = features.Installments;
                break;
            case "PurchaseDayOfWeek":
                value = features.PurchaseDayOfWeek;
                break;
            default:
                return null;
        }
        if (!float.IsFinite(value))
        {
            return null;
        }
        return value;
    }
}