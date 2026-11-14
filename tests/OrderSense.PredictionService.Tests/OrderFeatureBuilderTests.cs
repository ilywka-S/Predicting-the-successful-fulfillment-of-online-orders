using OrderSense.Contracts;

namespace OrderSense.PredictionService.Tests;

public class OrderFeatureBuilderTests
{
    private static readonly DateTime Monday = new DateTime(2018, 3, 5, 10, 30, 0);

    private static readonly DateTime Estimated = new DateTime(2018, 3, 15, 0, 0, 0);

    private static readonly (double Lat, double Lng) SaoPaulo = (-23.5505, -46.6333);

    private static readonly (double Lat, double Lng) Rio = (-22.9068, -43.1729);

    private static OrderFeatures Build(List<OrderItemInput> items, List<OrderPaymentInput> payments)
    {
        return OrderFeatureBuilder.Build(items, payments, "SP", "RJ", SaoPaulo, Rio, Monday, Estimated);
    }

    private static List<OrderItemInput> OneItem()
    {
        return [new OrderItemInput(100f, 15f, "health_beauty", 500f)];
    }

    private static List<OrderPaymentInput> OnePayment()
    {
        return [new OrderPaymentInput("credit_card", 3f, 115f)];
    }

    [Fact]
    public void Build_SumsItemsAndPrice()
    {
        List<OrderItemInput> items = [new OrderItemInput(100f, 10f, "a", 100f), new OrderItemInput(50f, 5f, "b", 200f)];

        OrderFeatures result = Build(items, OnePayment());

        Assert.Equal(2f, result.ItemsCount);
        Assert.Equal(150f, result.TotalPrice);
    }

    [Fact]
    public void Build_FreightShareIsFreightOverPriceAndFreight()
    {
        List<OrderItemInput> items = [new OrderItemInput(100f, 10f, "a", 100f), new OrderItemInput(50f, 5f, "b", 200f)];

        OrderFeatures result = Build(items, OnePayment());

        Assert.Equal(15f / 165f, result.FreightShare, 0.0001f);
        Assert.InRange(result.FreightShare, 0f, 1f);
    }

    [Fact]
    public void Build_FreightShareIsZeroWhenNothingIsPaid()
    {
        List<OrderItemInput> items = [new OrderItemInput(0f, 0f, "a", 100f)];

        OrderFeatures result = Build(items, OnePayment());

        Assert.Equal(0f, result.FreightShare);
    }

    [Fact]
    public void Build_WeightSumsOnlyKnownWeights()
    {
        List<OrderItemInput> items = [new OrderItemInput(100f, 10f, "a", 500f), new OrderItemInput(50f, 5f, "b", null)];

        OrderFeatures result = Build(items, OnePayment());

        Assert.Equal(500f, result.TotalWeightG);
    }

    [Fact]
    public void Build_WeightIsNaNWhenNoWeightIsKnown()
    {
        List<OrderItemInput> items = [new OrderItemInput(100f, 10f, "a", null)];

        OrderFeatures result = Build(items, OnePayment());

        Assert.True(float.IsNaN(result.TotalWeightG));
    }

    [Fact]
    public void Build_MainCategoryComesFromMostExpensiveItem()
    {
        List<OrderItemInput> items = [new OrderItemInput(50f, 5f, "cheap", 100f), new OrderItemInput(100f, 10f, "expensive", 100f)];

        OrderFeatures result = Build(items, OnePayment());

        Assert.Equal("expensive", result.MainCategory);
    }

    [Fact]
    public void Build_MissingCategoryBecomesEmptyString()
    {
        List<OrderItemInput> items = [new OrderItemInput(100f, 10f, null, 100f)];

        OrderFeatures result = Build(items, OnePayment());

        Assert.Equal("", result.MainCategory);
    }

    [Fact]
    public void Build_MainPaymentIsTheOneWithLargestValue()
    {
        List<OrderPaymentInput> payments = [new OrderPaymentInput("voucher", 1f, 20f), new OrderPaymentInput("credit_card", 3f, 100f)];

        OrderFeatures result = Build(OneItem(), payments);

        Assert.Equal("credit_card", result.PaymentType);
        Assert.Equal(3f, result.Installments);
    }

    [Fact]
    public void Build_WithoutPaymentsPaymentTypeIsEmptyAndInstallmentsUnknown()
    {
        OrderFeatures result = Build(OneItem(), []);

        Assert.Equal("", result.PaymentType);
        Assert.True(float.IsNaN(result.Installments));
    }

    [Fact]
    public void Build_DistanceBetweenSaoPauloAndRioIsAboutThreeHundredSixtyKilometers()
    {
        OrderFeatures result = Build(OneItem(), OnePayment());

        Assert.InRange(result.DistanceKm, 350f, 365f);
    }

    [Fact]
    public void Build_DistanceIsNaNWhenACoordinateIsMissing()
    {
        OrderFeatures withoutCustomer = OrderFeatureBuilder.Build(OneItem(), OnePayment(), "SP", "RJ", null, Rio, Monday, Estimated);
        OrderFeatures withoutSeller = OrderFeatureBuilder.Build(OneItem(), OnePayment(), "SP", "RJ", SaoPaulo, null, Monday, Estimated);

        Assert.True(float.IsNaN(withoutCustomer.DistanceKm));
        Assert.True(float.IsNaN(withoutSeller.DistanceKm));
    }

    [Fact]
    public void Build_PromisedDaysIgnoresTimeOfPurchase()
    {
        OrderFeatures result = Build(OneItem(), OnePayment());

        Assert.Equal(10f, result.PromisedDays);
    }

    [Fact]
    public void Build_PromisedDaysAndWeekdayAreNaNWithoutDates()
    {
        OrderFeatures result = OrderFeatureBuilder.Build(OneItem(), OnePayment(), "SP", "RJ", SaoPaulo, Rio, null, null);

        Assert.True(float.IsNaN(result.PromisedDays));
        Assert.True(float.IsNaN(result.PurchaseDayOfWeek));
    }

    [Theory]
    [InlineData(2018, 3, 4, 0f)]
    [InlineData(2018, 3, 5, 1f)]
    [InlineData(2018, 3, 10, 6f)]
    public void Build_WeekdayStartsWithSundayAsZero(int year, int month, int day, float expected)
    {
        OrderFeatures result = OrderFeatureBuilder.Build(OneItem(), OnePayment(), "SP", "RJ", SaoPaulo, Rio, new DateTime(year, month, day, 12, 0, 0), Estimated);

        Assert.Equal(expected, result.PurchaseDayOfWeek);
    }

    [Fact]
    public void Build_PassesStatesThrough()
    {
        OrderFeatures result = Build(OneItem(), OnePayment());

        Assert.Equal("SP", result.CustomerState);
        Assert.Equal("RJ", result.SellerState);
    }

    [Fact]
    public void Build_WithoutItemsReturnsEmptyOrder()
    {
        OrderFeatures result = Build([], OnePayment());

        Assert.Equal(0f, result.ItemsCount);
        Assert.Equal(0f, result.TotalPrice);
        Assert.Equal("", result.MainCategory);
        Assert.True(float.IsNaN(result.TotalWeightG));
    }
}