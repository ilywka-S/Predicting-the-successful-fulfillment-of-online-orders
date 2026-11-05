using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Tests.Infrastructure;
using OrderSense.OlistImport;

namespace OrderSense.Api.Tests.Import;

public sealed class OlistImportTests
{
    private static async Task RunImportAsync(AppDbContext db)
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Import", "Data");
        OlistImporter importer = new OlistImporter(db, folder);
        await importer.RunAsync();
    }

    [Fact]
    public async Task Import_SmallFiles_WritesExpectedRowCounts()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();

        await RunImportAsync(db);

        Assert.Equal(2, await db.GeoZips.CountAsync());
        Assert.Equal(3, await db.Customers.CountAsync());
        Assert.Equal(2, await db.Sellers.CountAsync());
        Assert.Equal(3, await db.Products.CountAsync());
        Assert.Equal(3, await db.Orders.CountAsync());
        Assert.Equal(3, await db.OrderItems.CountAsync());
        Assert.Equal(3, await db.OrderPayments.CountAsync());
    }

    [Fact]
    public async Task Import_ZipWithoutLeadingZero_IsPaddedAndCoordinatesAreAveraged()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();

        await RunImportAsync(db);

        GeoZip? geo = await db.GeoZips.FindAsync("01151");
        Customer? customer = await db.Customers.FindAsync("c1");

        Assert.NotNull(geo);
        Assert.NotNull(customer);
        Assert.Equal(-23.6, geo.Lat, 3);
        Assert.Equal(-46.7, geo.Lng, 3);
        Assert.Equal("01151", customer.ZipPrefix);
    }

    [Fact]
    public async Task Import_Categories_AreTranslatedOrLeftNull()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();

        await RunImportAsync(db);

        Product? translated = await db.Products.FindAsync("p1");
        Product? untranslated = await db.Products.FindAsync("p2");

        Assert.NotNull(translated);
        Assert.NotNull(untranslated);
        Assert.Equal("beleza_saude", translated.Category);
        Assert.Equal("health_beauty", translated.CategoryEn);
        Assert.Equal("categoria_sem_traducao", untranslated.Category);
        Assert.Null(untranslated.CategoryEn);
    }

    [Fact]
    public async Task Import_EmptyValues_BecomeNull()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();

        await RunImportAsync(db);

        Product? product = await db.Products.FindAsync("p3");
        Order? order = await db.Orders.FindAsync("o3");

        Assert.NotNull(product);
        Assert.NotNull(order);
        Assert.Null(product.Category);
        Assert.Null(product.WeightG);
        Assert.Null(product.PhotosQty);
        Assert.Null(order.ApprovedAt);
        Assert.Null(order.DeliveredCustomerAt);
    }

    [Fact]
    public async Task Import_Dates_AreStoredAsIs()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();

        await RunImportAsync(db);

        Order? order = await db.Orders.FindAsync("o1");

        Assert.NotNull(order);
        Assert.Equal(new DateTime(2018, 1, 10, 10, 0, 0), order.PurchasedAt);
        Assert.Equal(new DateTime(2018, 1, 25, 0, 0, 0), order.EstimatedDeliveryAt);
    }

    [Fact]
    public async Task Import_RowsWithUnknownReferences_AreRejectedWithoutBreakingImport()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();

        await RunImportAsync(db);

        Order? orderWithUnknownCustomer = await db.Orders.FindAsync("o4");
        OrderItem? itemWithUnknownProduct = await db.OrderItems.FindAsync("o3", 1);

        Assert.Null(orderWithUnknownCustomer);
        Assert.Null(itemWithUnknownProduct);
    }

    [Fact]
    public async Task Import_DuplicatePayment_IsRejected()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();

        await RunImportAsync(db);

        int paymentsOfSecondOrder = await db.OrderPayments.CountAsync(p => p.OrderId == "o2");

        Assert.Equal(1, paymentsOfSecondOrder);
    }

    [Fact]
    public async Task Import_UndefinedPaymentType_IsImportedAsIs()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();

        await RunImportAsync(db);

        OrderPayment? payment = await db.OrderPayments.FindAsync("o3", 1);

        Assert.NotNull(payment);
        Assert.Equal("not_defined", payment.Type);
        Assert.Equal(0, payment.Installments);
    }

    [Fact]
    public async Task Import_SecondRun_DoesNotDuplicateRows()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();

        await using (AppDbContext first = database.CreateContext())
        {
            await RunImportAsync(first);
        }

        await using AppDbContext second = database.CreateContext();
        await RunImportAsync(second);

        Assert.Equal(3, await second.Orders.CountAsync());
        Assert.Equal(3, await second.OrderItems.CountAsync());
        Assert.Equal(3, await second.OrderPayments.CountAsync());
        Assert.Equal(2, await second.GeoZips.CountAsync());
    }
}