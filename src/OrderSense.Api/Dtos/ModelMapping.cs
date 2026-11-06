using OrderSense.Contracts;
namespace OrderSense.Api.Dtos;
public static class ModelMapping
{
    public static ModelInfoDto ToDto(ModelDetails details)
    {
        List<FeatureImportanceDto> importances = new List<FeatureImportanceDto>();
        foreach (FeatureImportance item in details.FeatureImportances)
        {
            importances.Add(new FeatureImportanceDto(item.Feature, item.Importance));
        }
        return new ModelInfoDto(
            details.Version,
            details.TrainedAt,
            details.RocAuc,
            details.PrAuc,
            details.F1,
            details.BaselinePrAuc,
            details.TrainRows,
            details.TestRows,
            details.TrainPeriod,
            details.TestPeriod,
            details.RiskMediumFrom,
            details.RiskHighFrom,
            importances);
    }
}