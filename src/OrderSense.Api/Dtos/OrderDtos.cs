using OrderSense.Api.Data.Entities;

namespace OrderSense.Api.Dtos;

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public record OrderListItemDto(
    string Id,
    DateTime PurchaseAt,
    string Status,
    string CustomerState,
    int ItemsCount,
    decimal TotalValue,
    float? Probability,
    RiskLevel? RiskLevel);

public record OrderDetailsDto(
    string Id,
    string Status,
    DateTime PurchasedAt,
    DateTime? ApprovedAt,
    DateTime EstimatedDeliveryAt,
    DateTime? DeliveredCustomerAt,
    CustomerDto Customer,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<PaymentDto> Payments,
    PredictionDto? LatestPrediction);

public record CustomerDto(string? City, string State);

public record OrderItemDto(
    int ItemNo,
    string ProductId,
    string? Category,
    string SellerId,
    string SellerState,
    decimal Price,
    decimal FreightValue,
    int? WeightG);

public record PaymentDto(string Type, int Installments, decimal Value);

public record PredictionDto(
    float Probability,
    RiskLevel RiskLevel,
    string ModelVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyList<RiskFactorDto> Factors);

public enum FactorImpact
{
    Increases,
    Decreases
}

public record RiskFactorDto(string Feature, string Label, float? Value, FactorImpact Impact);    