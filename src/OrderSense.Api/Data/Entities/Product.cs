using System.ComponentModel.DataAnnotations;

namespace OrderSense.Api.Data.Entities;

public class Product
{
    [MaxLength(32)]
    public required string Id { get; set; }
    
    [MaxLength(100)]
    public string? Category { get; set; }
   
    [MaxLength(100)]
    public string? CategoryEn { get; set; }
    
    public int? WeightG { get; set; }
    
    public int? LengthCm { get; set; }
    
    public int? HeightCm { get; set; }
    
    public int? WidthCm { get; set; }
    
    public int? PhotosQty { get; set; }
}