using System.ComponentModel.DataAnnotations;
namespace OrderSense.Api.Dtos;
public record AnalyticsPointDto(
    string Key,
    int Orders,
    int Predicted,
    int Low,
    int Medium,
    int High,
    double? RiskyShare);
public class AnalyticsQuery : IValidatableObject
{
    [SupportedDate]
    public DateOnly? From 
    { 
        get; 
        init; 
    }
    [SupportedDate]
    public DateOnly? To 
    { 
        get; 
        init; 
    }
    [Range(1, 50)]
    public int Top 
    { 
        get; 
        init; 
    } = 10;
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From > To)
        {
            yield return new ValidationResult("'from' must not be later than 'to'", [nameof(From), nameof(To)]);
        }
    }
}