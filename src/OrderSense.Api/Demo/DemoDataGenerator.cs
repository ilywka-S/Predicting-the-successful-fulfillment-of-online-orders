using OrderSense.Api.Data.Entities;

namespace OrderSense.Api.Demo;

public sealed record DemoData(
    IReadOnlyList<GeoZip> GeoZips,
    IReadOnlyList<Seller> Sellers,
    IReadOnlyList<Product> Products,
    IReadOnlyList<Order> Orders);

public sealed class DemoDataGenerator
{
    private sealed record State(string Code, string Capital, int ZipFrom, int ZipTo, double Lat, double Lng, double CustomerWeight, double SellerWeight);

    private sealed record Category(string Pt, string En, decimal MinPrice, decimal MaxPrice, int MinWeightG, int MaxWeightG);

    private static readonly State[] States =
    [
        new("SP", "sao paulo", 1000, 19999, -23.55, -46.63, 42, 60),
        new("RJ", "rio de janeiro", 20000, 28999, -22.91, -43.17, 13, 5),
        new("MG", "belo horizonte", 30000, 39999, -19.92, -43.94, 12, 8),
        new("RS", "porto alegre", 90000, 99999, -30.03, -51.23, 5.5, 4),
        new("PR", "curitiba", 80000, 87999, -25.43, -49.27, 5, 8),
        new("SC", "florianopolis", 88000, 89999, -27.59, -48.55, 3.7, 5),
        new("BA", "salvador", 40000, 48999, -12.97, -38.50, 3.4, 1),
        new("DF", "brasilia", 70000, 72799, -15.79, -47.88, 2.1, 1),
        new("GO", "goiania", 74000, 76799, -16.68, -49.25, 2, 2),
        new("ES", "vitoria", 29000, 29999, -20.32, -40.34, 2, 1),
        new("PE", "recife", 50000, 56999, -8.05, -34.88, 1.7, 1),
        new("CE", "fortaleza", 60000, 63999, -3.73, -38.52, 1.3, 1),
    ];

    private static readonly Category[] Categories =
    [
        new("cama_mesa_banho", "bed_bath_table", 30, 200, 300, 3000),
        new("beleza_saude", "health_beauty", 15, 250, 100, 1500),
        new("esporte_lazer", "sports_leisure", 20, 400, 200, 5000),
        new("moveis_decoracao", "furniture_decor", 40, 600, 1000, 15000),
        new("informatica_acessorios", "computers_accessories", 25, 900, 100, 3000),
        new("utilidades_domesticas", "housewares", 15, 300, 200, 4000),
        new("relogios_presentes", "watches_gifts", 50, 800, 100, 800),
        new("telefonia", "telephony", 20, 1200, 100, 1000),
        new("ferramentas_jardim", "garden_tools", 30, 500, 500, 8000),
        new("automotivo", "auto", 20, 600, 300, 6000),
        new("brinquedos", "toys", 15, 300, 200, 3000),
    ];

    private static readonly (string Name, double Weight)[] Statuses = [("delivered", 96), ("shipped", 1.5), ("canceled", 1), ("unavailable", 0.7), ("invoiced", 0.4), ("processing", 0.4)];

    private static readonly (string Name, double Weight)[] PaymentTypes = [("credit_card", 74), ("boleto", 19), ("voucher", 5.5), ("debit_card", 1.5)];

    private static readonly DateTime PeriodStart = new(2017, 1, 1);
    private static readonly DateTime PeriodEnd = new(2018, 8, 31);

    private readonly Random _random;
    private readonly Dictionary<string, GeoZip> _geoZips = [];
    private readonly List<Seller> _sellers = [];
    private readonly List<Product> _products = [];

    public DemoDataGenerator(int seed = 42, int sellerCount = 50, int productCount = 300)
    {
        _random = new Random(seed);

        for (var i = 0; i < sellerCount; i++)
        {
            _sellers.Add(CreateSeller());
        }

        for (var i = 0; i < productCount; i++)
        {
            _products.Add(CreateProduct());
        }
    }

    public DemoData Generate(int orderCount)
    {
        var periodMinutes = (int)(PeriodEnd - PeriodStart).TotalMinutes;
        var orders = new List<Order>(orderCount);

        for (var i = 0; i < orderCount; i++)
        {
            orders.Add(CreateOrder(PeriodStart.AddMinutes(_random.Next(periodMinutes))));
        }

        return new DemoData([.. _geoZips.Values], _sellers, _products, orders);
    }

    public Order CreateOrder(DateTime purchasedAt)
    {
        var orderId = NextId();
        var state = Pick(States, s => s.CustomerWeight);
        var customer = new Customer
        {
            Id = NextId(), UniqueId = NextId(), ZipPrefix = CreateZip(state), City = state.Capital, State = state.Code,
        };

        var status = Pick(Statuses, s => s.Weight).Name;
        DateTime? approvedAt = purchasedAt.AddMinutes(_random.Next(10, 48 * 60));
        var estimatedDeliveryAt = purchasedAt.Date.AddDays(_random.Next(10, 36));
        DateTime? carrierAt = null;
        DateTime? deliveredAt = null;

        if (status is "shipped" or "delivered")
        {
            carrierAt = approvedAt.Value.AddHours(_random.Next(12, 96));
        }

        if (status == "delivered")
        {
            var promisedDays = (estimatedDeliveryAt - purchasedAt.Date).Days;
            var days = _random.NextDouble() < 0.08 ? promisedDays + _random.Next(1, 15) : _random.Next(7, promisedDays + 1);
            deliveredAt = purchasedAt.Date.AddDays(days).AddHours(_random.Next(8, 21));
        }

        var itemCount = _random.NextDouble() < 0.9 ? 1 : _random.Next(2, 4);
        var items = new List<OrderItem>(itemCount);

        for (var no = 1; no <= itemCount; no++)
        {
            var product = _products[_random.Next(_products.Count)];
            var category = Categories.First(c => c.Pt == product.Category);
            items.Add(new OrderItem
            {
                OrderId = orderId,
                ItemNo = no,
                ProductId = product.Id,
                SellerId = _sellers[_random.Next(_sellers.Count)].Id,
                ShippingLimitAt = approvedAt.Value.AddDays(_random.Next(2, 7)),
                Price = RandomMoney(category.MinPrice, category.MaxPrice),
                FreightValue = RandomMoney(7, 60),
            });
        }

        var paymentType = Pick(PaymentTypes, p => p.Weight).Name;

        return new Order
        {
            Id = orderId,
            CustomerId = customer.Id,
            Customer = customer,
            Status = status,
            PurchasedAt = purchasedAt,
            ApprovedAt = approvedAt,
            EstimatedDeliveryAt = estimatedDeliveryAt,
            DeliveredCarrierAt = carrierAt,
            DeliveredCustomerAt = deliveredAt,
            Items = items,
            Payments =
            [
                new OrderPayment
                {
                    OrderId = orderId,
                    Sequential = 1,
                    Type = paymentType,
                    Installments = paymentType == "credit_card" ? _random.Next(1, 11) : 1,
                    Value = items.Sum(i => i.Price + i.FreightValue),
                },
            ],
        };
    }

    private Seller CreateSeller()
    {
        var state = Pick(States, s => s.SellerWeight);

        return new Seller { Id = NextId(), ZipPrefix = CreateZip(state), City = state.Capital, State = state.Code };
    }

    private Product CreateProduct()
    {
        var category = Categories[_random.Next(Categories.Length)];

        return new Product
        {
            Id = NextId(),
            Category = category.Pt,
            CategoryEn = category.En,
            WeightG = _random.Next(category.MinWeightG, category.MaxWeightG + 1),
            LengthCm = _random.Next(10, 60),
            HeightCm = _random.Next(2, 40),
            WidthCm = _random.Next(10, 50),
            PhotosQty = _random.Next(1, 6),
        };
    }
    
    private string CreateZip(State state)
    {
        var zip = _random.Next(state.ZipFrom, state.ZipTo + 1).ToString("D5");

        if (!_geoZips.ContainsKey(zip))
        {
            _geoZips[zip] = new GeoZip
            {
                ZipPrefix = zip,
                Lat = Math.Round(state.Lat + (_random.NextDouble() - 0.5) * 2, 6),
                Lng = Math.Round(state.Lng + (_random.NextDouble() - 0.5) * 2, 6),
            };
        }

        return zip;
    }

    private string NextId()
    {
        Span<byte> bytes = stackalloc byte[16];
        _random.NextBytes(bytes);

        return Convert.ToHexStringLower(bytes);
    }

    private decimal RandomMoney(decimal min, decimal max) => Math.Round(min + (max - min) * (decimal)_random.NextDouble(), 2);

    private T Pick<T>(IReadOnlyList<T> items, Func<T, double> weight)
    {
        var roll = _random.NextDouble() * items.Sum(weight);

        foreach (var item in items)
        {
            roll -= weight(item);

            if (roll < 0)
            {
                return item;
            }
        }

        return items[^1];
    }
}