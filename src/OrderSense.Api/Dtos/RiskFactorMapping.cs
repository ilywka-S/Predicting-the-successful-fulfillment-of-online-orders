using System.Globalization;
using OrderSense.Api.Data.Entities;
namespace OrderSense.Api.Dtos;
public sealed record FactorContext(string? CustomerState, string? SellerState, string? Category, string? PaymentType);
public static class RiskFactorMapping
{
    public const int MaxFactors = 5;
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
    private static readonly string[] WeekDays = ["неділя", "понеділок", "вівторок", "середа", "четвер", "п'ятниця", "субота"];
    public static FactorContext CreateContext(CustomerDto customer, IReadOnlyList<OrderItemDto> items, IReadOnlyList<PaymentDto> payments)
    {
        Dictionary<string, decimal> sellerSums = new Dictionary<string, decimal>();
        foreach (OrderItemDto item in items)
        {
            sellerSums[item.SellerId] = sellerSums.GetValueOrDefault(item.SellerId) + item.Price;
        }
        string? sellerState = null;
        string? category = null;
        decimal bestSellerSum = decimal.MinValue;
        decimal bestPrice = decimal.MinValue;
        foreach (OrderItemDto item in items)
        {
            if (sellerSums[item.SellerId] > bestSellerSum)
            {
                bestSellerSum = sellerSums[item.SellerId];
                sellerState = item.SellerState;
            }
            if (item.Price > bestPrice)
            {
                bestPrice = item.Price;
                category = item.Category;
            }
        }
        string? paymentType = null;
        decimal bestPayment = decimal.MinValue;
        foreach (PaymentDto payment in payments)
        {
            if (payment.Value > bestPayment)
            {
                bestPayment = payment.Value;
                paymentType = payment.Type;
            }
        }
        return new FactorContext(customer.State, sellerState, category, paymentType);
    }
    public static List<RiskFactor> Top(IEnumerable<RiskFactor> factors, int count)
    {
        List<RiskFactor> sorted = new List<RiskFactor>(factors);
        sorted.Sort(CompareByImpact);
        if (sorted.Count > count)
        {
            sorted.RemoveRange(count, sorted.Count - count);
        }
        return sorted;
    }
    public static List<RiskFactorDto> ToDtos(IEnumerable<RiskFactor> factors, FactorContext? context)
    {
        List<RiskFactorDto> result = new List<RiskFactorDto>();
        foreach (RiskFactor factor in Top(factors, MaxFactors))
        {
            result.Add(ToDto(factor, context));
        }
        return result;
    }
    public static RiskFactorDto ToDto(RiskFactor factor)
    {
        return ToDto(factor, null);
    }
    public static RiskFactorDto ToDto(RiskFactor factor, FactorContext? context)
    {
        string label = Labels.GetValueOrDefault(factor.Feature, factor.Feature);
        FactorImpact impact = factor.Contribution > 0 ? FactorImpact.Increases : FactorImpact.Decreases;
        return new RiskFactorDto(factor.Feature, label, factor.Value, impact, FormatValue(factor.Feature, factor.Value, context));
    }
    private static string? FormatValue(string feature, float? value, FactorContext? context)
    {
        switch (feature)
        {
            case "CustomerState":
                return context?.CustomerState;
            case "SellerState":
                return context?.SellerState;
            case "MainCategory":
                return context?.Category;
            case "PaymentType":
                return context?.PaymentType;
        }
        if (value is null)
        {
            return null;
        }
        float v = value.Value;
        switch (feature)
        {
            case "ItemsCount":
                return $"{Whole(v)} шт.";
            case "TotalPrice":
                return $"{Money(v)} R$";
            case "FreightShare":
                return $"{Whole(v * 100)}%";
            case "TotalWeightG":
                if (v >= 1000)
                {
                    return $"{Decimal(v / 1000)} кг";
                }
                return $"{Whole(v)} г";
            case "PromisedDays":
                return $"{Whole(v)} дн.";
            case "DistanceKm":
                return $"{Whole(v)} км";
            case "Installments":
                return Whole(v);
            case "PurchaseDayOfWeek":
                int day = (int)Math.Round((double)v);
                if (day < 0 || day > 6)
                {
                    return null;
                }
                return WeekDays[day];
            default:
                return Decimal(v);
        }
    }
    private static string Whole(float v)
    {
        return Math.Round((double)v).ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ");
    }
    private static string Decimal(float v)
    {
        return v.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
    }
    private static string Money(float v)
    {
        return v.ToString("N2", CultureInfo.InvariantCulture).Replace(",", " ").Replace('.', ',');
    }
    private static int CompareByImpact(RiskFactor a, RiskFactor b)
    {
        int byImpact = Math.Abs(b.Contribution).CompareTo(Math.Abs(a.Contribution));
        if (byImpact != 0)
        {
            return byImpact;
        }
        return string.CompareOrdinal(a.Feature, b.Feature);
    }
}