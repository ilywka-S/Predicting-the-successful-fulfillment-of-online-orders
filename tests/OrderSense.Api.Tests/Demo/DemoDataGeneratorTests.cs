using System.Text.RegularExpressions;
using OrderSense.Api.Demo;
using OrderSense.Api.Data.Entities;

namespace OrderSense.Api.Tests.Demo;

public class DemoDataGeneratorTests
{
    private static readonly DemoData Data = new DemoDataGenerator(seed: 42).Generate(500);
    private static readonly Regex HexId = new("^[0-9a-f]{32}$");

    [Fact]
    public void Generate_SameSeed_ProducesSameOrders()
    {
        var again = new DemoDataGenerator(seed: 42).Generate(500);

        Assert.Equal(Data.Orders.Select(o => o.Id), again.Orders.Select(o => o.Id));
        Assert.Equal(Data.Orders.Select(o => o.Payments[0].Value), again.Orders.Select(o => o.Payments[0].Value));
    }

    [Fact]
    public void Generate_DifferentSeed_ProducesDifferentOrders()
    {
        var other = new DemoDataGenerator(seed: 7).Generate(500);

        Assert.NotEqual(Data.Orders.Select(o => o.Id), other.Orders.Select(o => o.Id));
    }

    [Fact]
    public void Generate_IdsAndCodesMatchOlistFormat()
    {
        Assert.All(Data.Orders, o =>
        {
            Assert.Matches(HexId, o.Id);
            Assert.Matches(HexId, o.Customer.Id);
            Assert.Matches("^[A-Z]{2}$", o.Customer.State);
            Assert.Matches(@"^\d{5}$", o.Customer.ZipPrefix);
        });
        Assert.All(Data.Sellers, s => Assert.Matches(@"^\d{5}$", s.ZipPrefix));
    }

    [Fact]
    public void Generate_ReferencesExistingSellersProductsAndZips()
    {
        var sellers = Data.Sellers.Select(s => s.Id).ToHashSet();
        var products = Data.Products.Select(p => p.Id).ToHashSet();
        var zips = Data.GeoZips.Select(z => z.ZipPrefix).ToHashSet();

        Assert.All(Data.Orders.SelectMany(o => o.Items), i =>
        {
            Assert.Contains(i.SellerId, sellers);
            Assert.Contains(i.ProductId, products);
        });
        Assert.All(Data.Orders, o => Assert.Contains(o.Customer.ZipPrefix, zips));
        Assert.All(Data.Sellers, s => Assert.Contains(s.ZipPrefix, zips));
    }

    [Fact]
    public void Generate_MoneyHasTwoDecimalsAndPaymentCoversOrder()
    {
        Assert.All(Data.Orders, o =>
        {
            Assert.NotEmpty(o.Items);
            Assert.All(o.Items, i =>
            {
                Assert.True(i.Price > 0 && decimal.Round(i.Price, 2) == i.Price);
                Assert.True(i.FreightValue > 0 && decimal.Round(i.FreightValue, 2) == i.FreightValue);
            });
            Assert.Equal(o.Items.Sum(i => i.Price + i.FreightValue), Assert.Single(o.Payments).Value);
        });
    }

    [Fact]
    public void Generate_DatesAreConsistent()
    {
        Assert.All(Data.Orders, o =>
        {
            Assert.True(o.ApprovedAt >= o.PurchasedAt);
            Assert.True(o.EstimatedDeliveryAt > o.PurchasedAt);

            if (o.DeliveredCarrierAt is { } carrier)
            {
                Assert.True(carrier >= o.ApprovedAt);
            }

            if (o.DeliveredCustomerAt is { } delivered)
            {
                Assert.True(delivered > o.DeliveredCarrierAt);
            }

            Assert.Equal(o.Status == "delivered", o.DeliveredCustomerAt is not null);
        });
    }

    [Fact]
    public void Generate_HasRealisticShareOfLateDeliveries()
    {
        var delivered = Data.Orders.Where(o => o.DeliveredCustomerAt is not null).ToList();
        var lateShare = delivered.Count(o => o.DeliveredCustomerAt!.Value.Date > o.EstimatedDeliveryAt.Date) / (double)delivered.Count;

        Assert.InRange(lateShare, 0.03, 0.15);
    }
    
    [Fact]
    public void CreateNewOrder_UsesGivenSellersAndProducts()
    {
        var product = new Product { Id = "p1", Category = "unknown_category" };
        var seller = new Seller { Id = "s1", ZipPrefix = "01001", State = "SP" };
        var generator = new DemoDataGenerator([seller], [product], seed: 1);
        var now = new DateTime(2026, 11, 8, 12, 0, 0);

        var order = generator.CreateNewOrder(now);

        Assert.All(order.Items, i =>
        {
            Assert.Equal("p1", i.ProductId);
            Assert.Equal("s1", i.SellerId);
        });
        Assert.Equal("approved", order.Status);
        Assert.Equal(now, order.PurchasedAt);
        Assert.Null(order.DeliveredCarrierAt);
        Assert.Null(order.DeliveredCustomerAt);
        Assert.True(generator.GeoZips.ContainsKey(order.Customer.ZipPrefix));
    }
}