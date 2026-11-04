namespace OrderSense.Api.Dtos;

public record ModelInfoDto(string Version, DateTimeOffset TrainedAt, float? RocAuc, float? PrAuc);