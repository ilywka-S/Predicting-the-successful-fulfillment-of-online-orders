using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Contracts;
namespace OrderSense.Api.Predictions;
public class WhatIfValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
public class WhatIfService(AppDbContext db, OrderFeatureLoader featureLoader,
    PredictionClient client,
    TimeProvider timeProvider,
    ILogger<WhatIfService> logger)
{
    private static readonly Action<ILogger, string, Exception?> ExplainFailed = LoggerMessage.Define<string>(LogLevel.Warning,
        new EventId(2, "WhatIfExplainFailed"),
        "Explain failed for what-if of order {OrderId}; result returned without risk factors");
    public async Task<WhatIfResultDto?> RunAsync(string orderId, WhatIfRequest request, CancellationToken ct)
    {
        FeatureOverrides overrides = await CreateOverridesAsync(request, ct);
        OrderFeatures? before = await featureLoader.LoadAsync(orderId, ct);
        if (before is null)
        {
            return null;
        }
        if (before.ItemsCount < 1)
        {
            throw new OrderNotPredictableException($"Order '{orderId}' has no items to predict");
        }
        OrderPrediction beforeResult = await client.PredictAsync(before, ct);
        OrderFeatures after = before;
        OrderPrediction afterResult = beforeResult;
        if (HasOverrides(request))
        {
            OrderFeatures? changed = await featureLoader.LoadAsync(orderId, overrides, ct);
            if (changed is null)
            {
                return null;
            }
            after = changed;
            afterResult = await client.PredictAsync(after, ct);
        }
        List<RiskFactorDto> factors = await LoadFactorsAsync(orderId, after, ct);
        PredictionDto beforeDto = ToDto(beforeResult, new List<RiskFactorDto>());
        PredictionDto afterDto = ToDto(afterResult, factors);
        return new WhatIfResultDto(beforeDto, afterDto);
    }
    private static bool HasOverrides(WhatIfRequest request)
    {
        return request.PromisedDays is not null || request.SellerId is not null
            || request.PaymentType is not null
            || request.Installments is not null;
    }
    private async Task<FeatureOverrides> CreateOverridesAsync(WhatIfRequest request, CancellationToken ct)
    {
        SellerOverride? seller = null;
        if (request.SellerId is not null)
        {
            Seller? found = await db.Sellers.FindAsync(new object[] { request.SellerId }, ct);
            if (found is null)
            {
                throw new WhatIfValidationException("sellerId", $"Seller '{request.SellerId}' not found");
            }

            seller = new SellerOverride(found.State, found.ZipPrefix);
        }
        return new FeatureOverrides(request.PromisedDays, seller, request.PaymentType, request.Installments);
    }
    private async Task<List<RiskFactorDto>> LoadFactorsAsync(string orderId, OrderFeatures features, CancellationToken ct)
    {
        List<RiskFactor> candidates = new List<RiskFactor>();
        try
        {
            OrderExplanation explanation = await client.ExplainAsync(features, ct);
            foreach (FeatureContribution item in explanation.Factors)
            {
                if (!float.IsFinite(item.Contribution))
                {
                    continue;
                }
                candidates.Add(new RiskFactor
                {
                    Feature = item.Feature,
                    Contribution = item.Contribution,
                    Value = FactorValues.Numeric(features, item.Feature),
                });
            }
        }
        catch (PredictionServiceUnavailableException e)
        {
            ExplainFailed(logger, orderId, e);
        }
        FactorContext context = new FactorContext(features.CustomerState, features.SellerState, features.MainCategory, features.PaymentType);
        return RiskFactorMapping.ToDtos(candidates, context);
    }
    private PredictionDto ToDto(OrderPrediction result, IReadOnlyList<RiskFactorDto> factors)
    {
        bool parsed = Enum.TryParse(result.RiskLevel, true, out RiskLevel riskLevel);
        if (!parsed || !Enum.IsDefined(riskLevel) || !float.IsFinite(result.Probability)
            || result.Probability < 0
            || result.Probability > 1
            || string.IsNullOrWhiteSpace(result.ModelVersion))
        {
            throw new PredictionServiceUnavailableException("Prediction service returned an invalid result");
        }
        return new PredictionDto(result.Probability, riskLevel, result.ModelVersion, timeProvider.GetUtcNow(), factors);
    }
}