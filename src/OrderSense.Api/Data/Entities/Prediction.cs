using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OrderSense.Api.Data.Entities;

[Index(nameof(OrderId), nameof(CreatedAt))]
public class Prediction
{
    public long Id { get; set; }

    [MaxLength(32)]
    public required string OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public int ModelVersionId { get; set; }

    public ModelVersion ModelVersion { get; set; } = null!;
    
    public float Probability { get; set; }

    public RiskLevel RiskLevel { get; set; }
    
    public List<RiskFactor> Factors { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
}