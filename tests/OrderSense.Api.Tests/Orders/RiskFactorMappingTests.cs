using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
namespace OrderSense.Api.Tests.Orders;
public class RiskFactorMappingTests
{
    [Theory]
    [InlineData("PromisedDays", 8f, "8 дн.")]
    [InlineData("DistanceKm", 2400f, "2 400 км")]
    [InlineData("TotalWeightG", 500f, "500 г")]
    [InlineData("TotalWeightG", 2500f, "2,5 кг")]
    [InlineData("FreightShare", 0.4f, "40%")]
    [InlineData("TotalPrice", 1234.5f, "1 234,50 R$")]
    [InlineData("ItemsCount", 3f, "3 шт.")]
    [InlineData("Installments", 10f, "10")]
    [InlineData("PurchaseDayOfWeek", 0f, "неділя")]
    [InlineData("PurchaseDayOfWeek", 1f, "понеділок")]
    public void ToDto_FormatsNumericValues(string feature, float value, string expected)
    {
        RiskFactor factor = new RiskFactor { Feature = feature, Value = value, Contribution = 0.1f };
        RiskFactorDto dto = RiskFactorMapping.ToDto(factor);
        Assert.Equal(expected, dto.DisplayValue);
    }
    [Fact]
    public void ToDto_PositiveContributionIncreasesRiskAndNegativeDecreasesIt()
    {
        RiskFactor up = new RiskFactor { Feature = "PromisedDays", Value = 8, Contribution = 0.2f };
        RiskFactor down = new RiskFactor { Feature = "PromisedDays", Value = 30, Contribution = -0.2f };
        Assert.Equal(FactorImpact.Increases, RiskFactorMapping.ToDto(up).Impact);
        Assert.Equal(FactorImpact.Decreases, RiskFactorMapping.ToDto(down).Impact);
    }
    [Fact]
    public void ToDto_CategoricalFeaturesUseOrderContext()
    {
        FactorContext context = new FactorContext("SP", "RJ", "health_beauty", "credit_card");
        Assert.Equal("SP", RiskFactorMapping.ToDto(new RiskFactor { Feature = "CustomerState", Contribution = 0.1f }, context).DisplayValue);
        Assert.Equal("RJ", RiskFactorMapping.ToDto(new RiskFactor { Feature = "SellerState", Contribution = 0.1f }, context).DisplayValue);
        Assert.Equal("health_beauty", RiskFactorMapping.ToDto(new RiskFactor { Feature = "MainCategory", Contribution = 0.1f }, context).DisplayValue);
        Assert.Equal("credit_card", RiskFactorMapping.ToDto(new RiskFactor { Feature = "PaymentType", Contribution = 0.1f }, context).DisplayValue);
    }
    [Fact]
    public void ToDto_UnknownFeatureKeepsItsName()
    {
        RiskFactor factor = new RiskFactor { Feature = "SomethingNew", Contribution = 0.1f };
        RiskFactorDto dto = RiskFactorMapping.ToDto(factor);
        Assert.Equal("SomethingNew", dto.Label);
        Assert.Null(dto.DisplayValue);
    }
}