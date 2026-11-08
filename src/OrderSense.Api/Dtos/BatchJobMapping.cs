using OrderSense.Api.Data.Entities;

namespace OrderSense.Api.Dtos;

public static class BatchJobMapping
{
    public static BatchJobDto ToDto(BatchJob job) => new(
        job.Id,
        job.FileName,
        job.Status,
        job.TotalRows,
        job.ProcessedRows,
        job.Error,
        job.CreatedAt,
        job.FinishedAt);

    public static BatchResultDto ToDto(BatchResult result) => new(
        result.RowNo,
        result.Probability,
        result.RiskLevel,
        TopFactors(result).Select(RiskFactorMapping.ToDto).ToList());

    public static IEnumerable<RiskFactor> TopFactors(BatchResult result) => result.Factors
        .OrderByDescending(f => Math.Abs(f.Contribution))
        .Take(5);
}