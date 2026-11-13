using System.ComponentModel.DataAnnotations;

namespace OrderSense.Api.Dtos;

public record WhatIfRequest(
    [Range(1, 365)] int? PromisedDays,
    [MaxLength(32)] string? SellerId,
    [AllowedValues(null, "credit_card", "boleto", "voucher", "debit_card")] string? PaymentType,
    [Range(1, 24)] int? Installments);

public record WhatIfResultDto(PredictionDto Before, PredictionDto After);