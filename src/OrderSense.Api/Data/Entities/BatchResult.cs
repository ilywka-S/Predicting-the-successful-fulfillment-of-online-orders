using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OrderSense.Api.Data.Entities;

[PrimaryKey(nameof(JobId), nameof(RowNo))]
public class BatchResult
{
    public Guid JobId { get; set; }

    public BatchJob Job { get; set; } = null!;

    public int RowNo { get; set; }

    [Column(TypeName = "jsonb")]
    public required string Input { get; set; }
    
    public float? Probability { get; set; }

    public RiskLevel? RiskLevel { get; set; }

    public List<RiskFactor> Factors { get; set; } = [];
}