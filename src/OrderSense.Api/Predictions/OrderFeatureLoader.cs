using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Contracts;

namespace OrderSense.Api.Predictions;

public class OrderFeatureLoader(AppDbContext db)
{
    public async Task<OrderFeatures?> LoadAsync(string orderId, CancellationToken ct)
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

        string[] zips = mainSeller is null ? [order.CustomerZip] : [order.CustomerZip, mainSeller.SellerZip];

        var coords = await db.GeoZips
            .AsNoTracking()
            .Where(z => zips.Contains(z.ZipPrefix))
            .ToDictionaryAsync(z => z.ZipPrefix, z => (z.Lat, z.Lng), ct);

        return OrderFeatureBuilder.Build(
            order.Items.Select(i => new OrderItemInput((float)i.Price, (float)i.FreightValue, i.Category, i.WeightG)).ToList(),
            order.Payments.Select(p => new OrderPaymentInput(p.Type, p.Installments, (float)p.Value)).ToList(),
            order.CustomerState,
            mainSeller?.SellerState ?? "",
            Coordinates(order.CustomerZip),
            mainSeller is null ? null : Coordinates(mainSeller.SellerZip),
            order.PurchasedAt,
            order.EstimatedDeliveryAt);

        (double Lat, double Lng)? Coordinates(string zip) => coords.TryGetValue(zip, out var c) ? c : null;
    }
}