using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OrderSense.Api.Data.Entities;

[Index(nameof(UniqueId))]
public class Customer
{
    [MaxLength(32)]
    public required string Id { get; set; }
    
    [MaxLength(32)]
    public required string UniqueId { get; set; }
    
    [MaxLength(5)]
    public required string ZipPrefix { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [Column(TypeName = "char(2)")]
    public required string State { get; set; }
    
    public Order? Order { get; set; }
}