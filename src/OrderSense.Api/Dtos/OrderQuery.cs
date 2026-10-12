using System.ComponentModel.DataAnnotations;
using OrderSense.Api.Data.Entities;

namespace OrderSense.Api.Dtos;

public enum OrderSort { DateDesc, DateAsc }

public class OrderQuery : IValidatableObject
{
    [AllowedValues(null, "created", "approved", "invoiced", "processing", "shipped", "delivered", "canceled", "unavailable")]
    public string? Status { get; init; }
    
    public DateOnly? From { get; init; }
    
    public DateOnly? To { get; init; }
    
    [RegularExpression("^[A-Z]{2}$")]
    public string? CustomerState { get; init; }
    
    public RiskLevel? RiskLevel { get; init; }
    
    [MaxLength(32)]
    public string? Search { get; init; }
    
    public OrderSort Sort { get; init; } = OrderSort.DateDesc;

    [Range(1, 100_000)] public int Page { get; init; } = 1;
    
    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (From > To)
        {
            yield return new ValidationResult("'from' must not be later than 'to'", [nameof(From), nameof(To)]);
        }
    }
}