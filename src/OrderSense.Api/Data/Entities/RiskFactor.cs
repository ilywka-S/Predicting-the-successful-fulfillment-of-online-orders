namespace OrderSense.Api.Data.Entities;

public class RiskFactor
{
    public required string Feature { get; set; }

    public float Contribution { get; set; }
    
    public float? Value { get; set; }
}