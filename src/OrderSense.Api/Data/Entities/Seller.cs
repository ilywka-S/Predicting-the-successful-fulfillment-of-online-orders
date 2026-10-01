using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderSense.Api.Data.Entities;

public class Seller
{
    [MaxLength(32)]
    public required string Id { get; set; }
    
    [MaxLength(5)]
    public required string ZipPrefix { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [Column(TypeName = "char(2)")]
    public required string State { get; set; }
}