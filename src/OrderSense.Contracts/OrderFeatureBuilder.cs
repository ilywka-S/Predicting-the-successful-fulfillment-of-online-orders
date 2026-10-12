namespace OrderSense.Contracts;

public sealed record OrderItemInput(float Price, float FreightValue, string? CategoryName, float? WeightG);
public sealed record OrderPaymentInput(string PaymentType, float Installments, float Value);

public static class OrderFeatureBuilder
{
    public static OrderFeatures Build(
        IReadOnlyList<OrderItemInput> items,
        IReadOnlyList<OrderPaymentInput> payments,
        string customerState,
        string sellerState,
        (double Lat, double Lng)? customerCoords,
        (double Lat, double Lng)? sellerCoords,
        DateTime? purchaseDate,
        DateTime? estimatedDeliveryDate)
    {
        var totalPrice = items.Sum(i => i.Price);
        var totalFreight = items.Sum(i => i.FreightValue);
        var totalPriceAndFreight = totalPrice + totalFreight;

        var totalWeight = items.Any(i => i.WeightG.HasValue)
            ? items.Where(i => i.WeightG.HasValue).Sum(i => i.WeightG!.Value)
            : float.NaN;

        var mainItem = items.OrderByDescending(i => i.Price).FirstOrDefault();
        var mainPayment = payments.OrderByDescending(p => p.Value).FirstOrDefault();

        var distanceKm = float.NaN;
        if (customerCoords.HasValue && sellerCoords.HasValue)
        {
            distanceKm = (float)HaversineKm(customerCoords.Value.Lat, customerCoords.Value.Lng,
                sellerCoords.Value.Lat, sellerCoords.Value.Lng);
        }

        var promisedDays = float.NaN;
        if (purchaseDate.HasValue && estimatedDeliveryDate.HasValue)
        {
            promisedDays = (float)(estimatedDeliveryDate.Value.Date - purchaseDate.Value.Date).TotalDays;
        }

        return new OrderFeatures
        {
            ItemsCount = items.Count,
            TotalPrice = totalPrice,
            FreightShare = totalPriceAndFreight > 0 ? totalFreight / totalPriceAndFreight : 0,
            TotalWeightG = totalWeight,
            PromisedDays = promisedDays,
            DistanceKm = distanceKm,
            CustomerState = customerState,
            SellerState = sellerState,
            MainCategory = mainItem?.CategoryName ?? "",
            PaymentType = mainPayment?.PaymentType ?? "",
            Installments = mainPayment?.Installments ?? float.NaN,
            PurchaseDayOfWeek = purchaseDate.HasValue ? (float)(int)purchaseDate.Value.DayOfWeek : float.NaN,
        };
    }

    private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLng = (lng2 - lng1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }
}