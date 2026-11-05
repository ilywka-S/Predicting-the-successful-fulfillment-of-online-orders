using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Olist;

namespace OrderSense.OlistImport;

public sealed class OlistImporter(AppDbContext db, string folder)
{
    private const int BatchSize = 5000;

    public async Task RunAsync()
    {
        await ImportAsync("geo_zip", db.GeoZips, BuildGeoZipsAsync);
        await ImportAsync("customers", db.Customers, BuildCustomersAsync);
        await ImportAsync("sellers", db.Sellers, BuildSellersAsync);
        await ImportAsync("products", db.Products, BuildProductsAsync);
        await ImportAsync("orders", db.Orders, BuildOrdersAsync);
        await ImportAsync("order_items", db.OrderItems, BuildOrderItemsAsync);
        await ImportAsync("order_payments", db.OrderPayments, BuildOrderPaymentsAsync);
        await PrintSummaryAsync();
    }
    //одна таблиця = одна транзакція; непорожню таблицю пропускаємо.
    private async Task ImportAsync<T>(string table, DbSet<T> set, Func<Task<(List<T> Rows, int Read, Rejects Rejects)>> build) where T : class
    {
        if (await set.AnyAsync())
        {
            Console.WriteLine($"{table}: вже імпортовано, пропускаю");
            return;
        }
        var (rows, read, rejects) = await build();
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i += BatchSize)
        {
            set.AddRange(rows.Skip(i).Take(BatchSize));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
        await tx.CommitAsync();
        Console.WriteLine($"{table}: прочитано {read}, записано {rows.Count}, відкинуто {rejects.Total}");
        foreach (var (reason, count) in rejects.Items)
        { 
            Console.WriteLine($"    відкинуто {count}: {reason}");
        }
    }
    private static string Zip(string? z) => (z ?? "").Trim().PadLeft(5, '0');
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static int? ToInt(float? v) => v is null ? null : (int)Math.Round(v.Value);
    private Task<(List<GeoZip>, int, Rejects)> BuildGeoZipsAsync()
    {
        var source = OlistCsv.Read<GeolocationRow>(folder, "olist_geolocation_dataset.csv");
        Console.WriteLine($"geo_zip: у файлі {source.Count} точок, усереднюю по префіксу");
        var rows = source
            .GroupBy(g => Zip(g.ZipCodePrefix))
            .Select(g => new GeoZip
            {
                ZipPrefix = g.Key,
                Lat = g.Average(x => x.Lat),
                Lng = g.Average(x => x.Lng),
            })
            .ToList();
        return Task.FromResult((rows, rows.Count, new Rejects()));
    }
    private Task<(List<Customer>, int, Rejects)> BuildCustomersAsync()
    {
        var rejects = new Rejects();
        var source = OlistCsv.Read<CustomerRow>(folder, "olist_customers_dataset.csv");
        var seen = new HashSet<string>();
        var rows = new List<Customer>();
        foreach (var r in source)
        {
            var zip = Zip(r.CustomerZipCodePrefix);
            var state = r.CustomerState?.Trim();
            if (zip.Length > 5) 
            { 
                rejects.Add("zip довший за 5 символів"); 
                continue; 
            }
            if (state is not { Length: 2 }) 
            { 
                rejects.Add("штат не з 2 символів"); 
                continue; 
            }
            if (!seen.Add(r.CustomerId)) 
            { 
                rejects.Add("дублікат customer_id"); 
                continue; 
            }
            rows.Add(new Customer
            {
                Id = r.CustomerId,
                UniqueId = r.CustomerUniqueId,
                ZipPrefix = zip,
                City = Clean(r.CustomerCity),
                State = state,
            });
        }
        return Task.FromResult((rows, source.Count, rejects));
    }
    private Task<(List<Seller>, int, Rejects)> BuildSellersAsync()
    {
        var rejects = new Rejects();
        var source = OlistCsv.Read<SellerRow>(folder, "olist_sellers_dataset.csv");
        var seen = new HashSet<string>();
        var rows = new List<Seller>();
        foreach (var r in source)
        {
            var zip = Zip(r.SellerZipCodePrefix);
            var state = r.SellerState?.Trim();
            if (zip.Length > 5) 
            { 
                rejects.Add("zip довший за 5 символів"); 
                continue; 
            }
            if (state is not { Length: 2 }) 
            { 
                rejects.Add("штат не з 2 символів"); 
                continue; 
            }
            if (!seen.Add(r.SellerId)) 
            { 
                rejects.Add("дублікат seller_id"); 
                continue; 
            }
            rows.Add(new Seller
            {
                Id = r.SellerId,
                ZipPrefix = zip,
                City = Clean(r.SellerCity),
                State = state,
            });
        }
        return Task.FromResult((rows, source.Count, rejects));
    }
    private Task<(List<Product>, int, Rejects)> BuildProductsAsync()
    {
        var rejects = new Rejects();
        var translations = OlistCsv
            .Read<CategoryTranslationRow>(folder, "product_category_name_translation.csv")
            .GroupBy(t => t.Category.Trim())
            .ToDictionary(g => g.Key, g => g.First().CategoryEn.Trim());

        var source = OlistCsv.Read<ProductRow>(folder, "olist_products_dataset.csv");
        var seen = new HashSet<string>();
        var rows = new List<Product>();

        foreach (var r in source)
        {
            if (!seen.Add(r.ProductId)) 
            { 
                rejects.Add("дублікат product_id"); 
                continue; 
            }
            var category = Clean(r.ProductCategoryName);
            string? categoryEn = null;
            if (category is not null && translations.TryGetValue(category, out var en))
            { 
                categoryEn = en;
            }
            rows.Add(new Product
            {
                Id = r.ProductId,
                Category = category,
                CategoryEn = categoryEn,
                WeightG = ToInt(r.ProductWeightG),
                LengthCm = ToInt(r.ProductLengthCm),
                HeightCm = ToInt(r.ProductHeightCm),
                WidthCm = ToInt(r.ProductWidthCm),
                PhotosQty = r.ProductPhotosQty,
            });
        }
        return Task.FromResult((rows, source.Count, rejects));
    }
    private async Task<(List<Order>, int, Rejects)> BuildOrdersAsync()
    {
        var rejects = new Rejects();
        var customerIds = (await db.Customers.Select(c => c.Id).ToListAsync()).ToHashSet();
        var source = OlistCsv.Read<OrderRow>(folder, "olist_orders_dataset.csv");
        var seen = new HashSet<string>();
        var usedCustomers = new HashSet<string>();
        var rows = new List<Order>();
        foreach (var r in source)
        {
            if (!customerIds.Contains(r.CustomerId)) 
            { 
                rejects.Add("customer_id немає в customers"); 
                continue; 
            }
            if (r.OrderPurchaseTimestamp is null) 
            { 
                rejects.Add("немає дати покупки"); 
                continue; 
            }
            if (r.OrderEstimatedDeliveryDate is null) 
            { 
                rejects.Add("немає обіцяної дати доставки"); 
                continue; 
            }
            if (!seen.Add(r.OrderId)) 
            { 
                rejects.Add("дублікат order_id"); 
                continue; 
            }
            if (!usedCustomers.Add(r.CustomerId)) 
            { 
                rejects.Add("customer_id уже в іншому замовленні"); 
                continue; 
            }
            rows.Add(new Order
            {
                Id = r.OrderId,
                CustomerId = r.CustomerId,
                Status = r.OrderStatus.Trim(),
                PurchasedAt = r.OrderPurchaseTimestamp.Value,
                ApprovedAt = r.OrderApprovedAt,
                EstimatedDeliveryAt = r.OrderEstimatedDeliveryDate.Value,
                DeliveredCarrierAt = r.OrderDeliveredCarrierDate,
                DeliveredCustomerAt = r.OrderDeliveredCustomerDate,
            });
        }
        return (rows, source.Count, rejects);
    }
    private async Task<(List<OrderItem>, int, Rejects)> BuildOrderItemsAsync()
    {
        var rejects = new Rejects();
        var orderIds = (await db.Orders.Select(o => o.Id).ToListAsync()).ToHashSet();
        var productIds = (await db.Products.Select(p => p.Id).ToListAsync()).ToHashSet();
        var sellerIds = (await db.Sellers.Select(s => s.Id).ToListAsync()).ToHashSet();
        var source = OlistCsv.Read<OrderItemRow>(folder, "olist_order_items_dataset.csv");
        var seen = new HashSet<(string, int)>();
        var rows = new List<OrderItem>();
        foreach (var r in source)
        {
            if (!orderIds.Contains(r.OrderId)) 
            { 
                rejects.Add("order_id немає в orders"); 
                continue; 
            }
            if (!productIds.Contains(r.ProductId)) 
            { 
                rejects.Add("product_id немає в products"); 
                continue; 
            }
            if (!sellerIds.Contains(r.SellerId)) 
            { 
                rejects.Add("seller_id немає в sellers"); 
                continue; 
            }
            if (!seen.Add((r.OrderId, r.OrderItemId))) { rejects.Add("дублікат (order_id, order_item_id)"); continue; }
            rows.Add(new OrderItem
            {
                OrderId = r.OrderId,
                ItemNo = r.OrderItemId,
                ProductId = r.ProductId,
                SellerId = r.SellerId,
                ShippingLimitAt = r.ShippingLimitDate,
                Price = r.Price,
                FreightValue = r.FreightValue,
            });
        }
        return (rows, source.Count, rejects);
    }
    private async Task<(List<OrderPayment>, int, Rejects)> BuildOrderPaymentsAsync()
    {
        var rejects = new Rejects();
        var orderIds = (await db.Orders.Select(o => o.Id).ToListAsync()).ToHashSet();
        var source = OlistCsv.Read<PaymentRow>(folder, "olist_order_payments_dataset.csv");
        var seen = new HashSet<(string, int)>();
        var rows = new List<OrderPayment>();
        foreach (var r in source)
        {
            if (!orderIds.Contains(r.OrderId)) 
            { 
                rejects.Add("order_id немає в orders"); 
                continue; 
            }
            if (!seen.Add((r.OrderId, r.PaymentSequential))) 
            { 
                rejects.Add("дублікат (order_id, payment_sequential)"); 
                continue; 
            }
            rows.Add(new OrderPayment
            {
                OrderId = r.OrderId,
                Sequential = r.PaymentSequential,
                Type = r.PaymentType.Trim(),
                Installments = r.PaymentInstallments,
                Value = r.PaymentValue,
            });
        }
        return (rows, source.Count, rejects);
    }
    private async Task PrintSummaryAsync()
    {
        Console.WriteLine();
        Console.WriteLine("Рядків у базі:");
        Console.WriteLine($"  geo_zip: {await db.GeoZips.CountAsync()}");
        Console.WriteLine($"  customers: {await db.Customers.CountAsync()}");
        Console.WriteLine($"  sellers: {await db.Sellers.CountAsync()}");
        Console.WriteLine($"  products: {await db.Products.CountAsync()}");
        Console.WriteLine($"  orders: {await db.Orders.CountAsync()}");
        Console.WriteLine($"  order_items: {await db.OrderItems.CountAsync()}");
        Console.WriteLine($"  order_payments: {await db.OrderPayments.CountAsync()}");
        var sizes = await db.Database
            .SqlQueryRaw<string>("select pg_size_pretty(pg_database_size(current_database())) as \"Value\"")
            .ToListAsync();

        Console.WriteLine($"Розмір бази: {sizes[0]}");
    }
}
