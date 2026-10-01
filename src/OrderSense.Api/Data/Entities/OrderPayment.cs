using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace OrderSense.Api.Data.Entities;

[PrimaryKey(nameof(OrderId), nameof(Sequential))]
public class OrderPayment
{
    [MaxLength(32)]
    public required string OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public int Sequential { get; set; }
    
    [MaxLength(20)]
    public required string Type { get; set; }

    public int Installments { get; set; }

    [Precision(10, 2)]
    public decimal Value { get; set; }
}