using OrderSense.Api.Data.Entities;

namespace OrderSense.Api.Dtos;

public record BatchJobDto(
    Guid Id,
    string FileName,
    BatchJobStatus Status,
    int TotalRows,
    int ProcessedRows,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt);

public record BatchResultDto(
    int RowNo,
    float? Probability,
    RiskLevel? RiskLevel,
    IReadOnlyList<RiskFactorDto> Factors);