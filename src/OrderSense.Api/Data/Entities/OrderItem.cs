using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OrderSense.Api.Data.Entities;

[PrimaryKey(nameof(OrderId), nameof(ItemNo))]
public class OrderItem
{
    [MaxLength(32)]
    public required string OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public int ItemNo { get; set; }

    [MaxLength(32)]
    public required string ProductId { get; set; }

    public Product Product { get; set; } = null!;

    [MaxLength(32)]
    public required string SellerId { get; set; }

    public Seller Seller { get; set; } = null!;

    [Column(TypeName = "timestamp without time zone")]
    public DateTime? ShippingLimitAt { get; set; }

    [Precision(10, 2)]
    public decimal Price { get; set; }

    [Precision(10, 2)]
    public decimal FreightValue { get; set; }
}