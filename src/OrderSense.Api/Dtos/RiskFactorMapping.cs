using OrderSense.Api.Data.Entities;

namespace OrderSense.Api.Dtos;

public static class RiskFactorMapping
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["ItemsCount"] = "Кількість товарів",
        ["TotalPrice"] = "Сума замовлення",
        ["FreightShare"] = "Частка вартості доставки",
        ["TotalWeightG"] = "Загальна вага",
        ["PromisedDays"] = "Обіцяний термін доставки",
        ["DistanceKm"] = "Відстань продавець -> клієнт",
        ["CustomerState"] = "Штат клієнта",
        ["SellerState"] = "Штат продавця",
        ["MainCategory"] = "Категорія товару",
        ["PaymentType"] = "Спосіб оплати",
        ["Installments"] = "Кількість платежів",
        ["PurchaseDayOfWeek"] = "День тижня покупки"
    };

    public static RiskFactorDto ToDto(RiskFactor factor) => new(
        factor.Feature,
        Labels.GetValueOrDefault(factor.Feature, factor.Feature),
        factor.Value,
        factor.Contribution > 0 ? FactorImpact.Decreases : FactorImpact.Increases);
}