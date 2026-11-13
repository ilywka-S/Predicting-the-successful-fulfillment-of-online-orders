namespace OrderSense.Api.Dtos;

public record FeatureImportanceDto(string Feature, float Importance);

public record ModelInfoDto(
    string Version,
    DateTimeOffset TrainedAt,
    float? RocAuc,
    float? PrAuc,
    float F1,
    float BaselinePrAuc,
    int TrainRows,
    int TestRows,
    string TrainPeriod,
    string TestPeriod,
    float RiskMediumFrom,
    float RiskHighFrom,
    IReadOnlyList<FeatureImportanceDto> FeatureImportances);