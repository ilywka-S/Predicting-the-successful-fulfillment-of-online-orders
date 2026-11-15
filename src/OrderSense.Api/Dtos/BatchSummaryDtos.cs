namespace OrderSense.Api.Dtos;

public record BatchJobSummaryDto(
    Guid JobId,
    string FileName,
    int TotalRows,
    int PredictedRows,
    int Low,
    int Medium,
    int High,
    double? AverageProbability,
    IReadOnlyList<AnalyticsPointDto> ByCategory,
    IReadOnlyList<AnalyticsPointDto> ByCustomerState,
    IReadOnlyList<AnalyticsPointDto> ByPaymentType,
    IReadOnlyList<BatchFactorCountDto> TopFactors);

public record BatchFactorCountDto(
    string Feature,
    string Label,
    int Rows);
