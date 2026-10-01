using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OrderSense.Api.Data.Entities;

[Index(nameof(Version), IsUnique = true)]
public class ModelVersion
{
    public int Id { get; set; }

    [MaxLength(50)]
    public required string Version { get; set; }

    public DateTimeOffset TrainedAt { get; set; }

    public float? RocAuc { get; set; }

    public float? PrAuc { get; set; }

    public bool IsActive { get; set; }
}