using OrderSense.Contracts;

namespace OrderSense.PredictionService.Tests;

public sealed class RiskThresholdsTests
{
    [Theory]
    [InlineData(0.05f, "low")]
    [InlineData(0.12f, "medium")]
    [InlineData(0.15f, "medium")]
    [InlineData(0.20f, "high")]
    [InlineData(0.90f, "high")]
    public void Level_maps_problem_probability(float pProblem, string expected) =>
        Assert.Equal(expected, new RiskThresholds().Level(pProblem));
}

public sealed class PredictValidatorTests
{
    private static OrderFeatures Valid() => new()
    {
        ItemsCount = 1, TotalPrice = 100, FreightShare = 0.1f, TotalWeightG = 500,
        PromisedDays = 15, DistanceKm = 300, CustomerState = "SP", SellerState = "SP",
        MainCategory = "x", PaymentType = "boleto", Installments = 1, PurchaseDayOfWeek = 3
    };

    [Fact]
    public void Valid_features_have_no_errors() =>
        Assert.Empty(PredictValidator.Validate(Valid()));

    [Fact]
    public void NaN_is_allowed_for_unknown_values()
    {
        var f = Valid();
        f.TotalWeightG = float.NaN;
        f.DistanceKm = float.NaN;
        f.Installments = float.NaN;
        f.PromisedDays = float.NaN;
        f.PurchaseDayOfWeek = float.NaN;

        Assert.Empty(PredictValidator.Validate(f));
    }

    [Fact]
    public void Out_of_range_values_are_rejected()
    {
        var f = Valid();
        f.ItemsCount = 0;
        f.TotalPrice = -1;
        f.FreightShare = 1.5f;
        f.PurchaseDayOfWeek = 7;
        f.TotalWeightG = float.PositiveInfinity;

        var errors = PredictValidator.Validate(f);

        Assert.Contains(nameof(f.ItemsCount), errors.Keys);
        Assert.Contains(nameof(f.TotalPrice), errors.Keys);
        Assert.Contains(nameof(f.FreightShare), errors.Keys);
        Assert.Contains(nameof(f.PurchaseDayOfWeek), errors.Keys);
        Assert.Contains(nameof(f.TotalWeightG), errors.Keys);
    }
}