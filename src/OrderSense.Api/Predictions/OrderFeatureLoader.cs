using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Contracts;

namespace OrderSense.Api.Predictions;

public sealed record SellerOverride(string State, string ZipPrefix);
public sealed record FeatureOverrides(int? PromisedDays, SellerOverride? Seller, string? PaymentType, int? Installments);
public class OrderFeatureLoader(AppDbContext db)
{
    public Task<OrderFeatures?> LoadAsync(string orderId, CancellationToken ct)
    {
        return LoadAsync(orderId, null, ct);
    }
    public async Task<OrderFeatures?> LoadAsync(string orderId, FeatureOverrides? overrides, CancellationToken ct)
    {
        var order = await db.Orders
            .AsNoTracking()
            .AsSingleQuery()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.PurchasedAt,
                o.EstimatedDeliveryAt,
                CustomerState = o.Customer.State,
                CustomerZip = o.Customer.ZipPrefix,
                Items = o.Items
                    .OrderBy(i => i.ItemNo)
                    .Select(i => new
                    {
                        i.Price,
                        i.FreightValue,
                        i.Product.Category,
                        i.Product.WeightG,
                        i.SellerId,
                        SellerState = i.Seller.State,
                        SellerZip = i.Seller.ZipPrefix,
                    })
                    .ToList(),
                Payments = o.Payments
                    .OrderBy(p => p.Sequential)
                    .Select(p => new { p.Type, p.Installments, p.Value })
                    .ToList(),
            })
            .SingleOrDefaultAsync(ct);
        if (order is null)
        {
            return null;
        }
        var mainSeller = order.Items
            .GroupBy(i => i.SellerId)
            .OrderByDescending(g => g.Sum(i => i.Price))
            .Select(g => g.First())
            .FirstOrDefault();
        string? sellerState = mainSeller?.SellerState;
        string? sellerZip = mainSeller?.SellerZip;
        if (overrides?.Seller is { } seller)
        {
            sellerState = seller.State;
            sellerZip = seller.ZipPrefix;
        }
        string[] zips = sellerZip is null ? [order.CustomerZip] : [order.CustomerZip, sellerZip];
        var coords = await db.GeoZips
            .AsNoTracking()
            .Where(z => zips.Contains(z.ZipPrefix))
            .ToDictionaryAsync(z => z.ZipPrefix, z => (z.Lat, z.Lng), ct);
        List<OrderPaymentInput> paymentInputs = order.Payments
            .Select(p => new OrderPaymentInput(p.Type, p.Installments, (float)p.Value))
            .ToList();
        ApplyPaymentOverrides(paymentInputs, overrides);
        DateTime estimatedDelivery = order.EstimatedDeliveryAt;
        if (overrides?.PromisedDays is { } promisedDays)
        {
            estimatedDelivery = order.PurchasedAt.Date.AddDays(promisedDays);
        }
        return OrderFeatureBuilder.Build(
            order.Items.Select(i => new OrderItemInput((float)i.Price, (float)i.FreightValue, i.Category, i.WeightG)).ToList(),
            paymentInputs,
            order.CustomerState,
            sellerState ?? "",
            Coordinates(order.CustomerZip),
            sellerZip is null ? null : Coordinates(sellerZip),
            order.PurchasedAt,
            estimatedDelivery);
        (double Lat, double Lng)? Coordinates(string zip) => coords.TryGetValue(zip, out var c) ? c : null;
    }
    private static void ApplyPaymentOverrides(List<OrderPaymentInput> payments, FeatureOverrides? overrides)
    {
        if (overrides is null || (overrides.PaymentType is null && overrides.Installments is null))
        {
            return;
        }
        if (payments.Count == 0)
        {
            payments.Add(new OrderPaymentInput(overrides.PaymentType ?? "", overrides.Installments ?? 0, 0f));

            return;
        }
        int mainIndex = 0;
        for (int i = 1; i < payments.Count; i++)
        {
            if (payments[i].Value > payments[mainIndex].Value)
            {
                mainIndex = i;
            }
        }
        OrderPaymentInput main = payments[mainIndex];
        string type = overrides.PaymentType ?? main.PaymentType;
        float installments = main.Installments;

        if (overrides.Installments is { } value)
        {
            installments = value;
        }
        payments[mainIndex] = new OrderPaymentInput(type, installments, main.Value);
    }
}