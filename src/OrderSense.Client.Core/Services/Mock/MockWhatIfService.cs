using OrderSense.Client.Core.Interfaces;

namespace OrderSense.Client.Core.Services.Mock;

public class MockWhatIfService : IWhatIfService
{
    public async Task<WhatIfResultDto> RunAsync(string orderId, WhatIfRequest request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(400, cancellationToken);

        var before = 0.55 + (Math.Abs(orderId.GetHashCode(StringComparison.Ordinal) % 30) / 100.0);
        var after = before;

        if (request.PromisedDays is { } days)
        {
            after += Math.Clamp((days - 20) * 0.01, -0.2, 0.15);
        }

        after += request.PaymentType switch
        {
            "boleto" => -0.04,
            "voucher" => -0.02,
            "debit_card" => 0.01,
            _ => 0,
        };

        if (request.Installments is > 6)
        {
            after -= 0.03;
        }

        if (!string.IsNullOrEmpty(request.SellerId))
        {
            after += 0.05;
        }

        return new WhatIfResultDto
        {
            Before = Prediction(before, []),
            After = Prediction(Math.Clamp(after, 0.05, 0.98), Factors(request)),
        };
    }

    private static PredictionDto Prediction(double probability, List<RiskFactorDto> factors) => new()
    {
        Probability = (float)probability,
        RiskLevel = probability >= 0.8 ? RiskLevel.Low : probability >= 0.6 ? RiskLevel.Medium : RiskLevel.High,
        ModelVersion = "mock",
        CreatedAt = DateTimeOffset.Now,
        Factors = factors,
    };

    private static List<RiskFactorDto> Factors(WhatIfRequest request)
    {
        var factors = new List<RiskFactorDto>();

        if (request.PromisedDays is { } days)
        {
            factors.Add(new RiskFactorDto
            {
                Feature = "PromisedDays",
                Label = "Обіцяний термін доставки",
                Value = days,
                Impact = days >= 20 ? FactorImpact.Decreases : FactorImpact.Increases,
            });
        }

        if (request.PaymentType is { } payment)
        {
            factors.Add(new RiskFactorDto
            {
                Feature = "PaymentType",
                Label = "Спосіб оплати",
                Impact = payment == "boleto" ? FactorImpact.Increases : FactorImpact.Decreases,
            });
        }

        if (request.Installments is { } installments)
        {
            factors.Add(new RiskFactorDto
            {
                Feature = "Installments",
                Label = "Кількість платежів",
                Value = installments,
                Impact = installments > 6 ? FactorImpact.Increases : FactorImpact.Decreases,
            });
        }

        return factors;
    }
}
