using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OrderSense.Api.Data.Entities;

[Index(nameof(PurchasedAt))]
[Index(nameof(Status))]
public class Order
{
    [MaxLength(32)]
    public required string Id { get; set; }

    [MaxLength(32)]
    public required string CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;
    
    [MaxLength(20)]
    public required string Status { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime PurchasedAt { get; set; }
    
    [Column(TypeName = "timestamp without time zone")]
    public DateTime? ApprovedAt { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime EstimatedDeliveryAt { get; set; }
    
    [Column(TypeName = "timestamp without time zone")]
    public DateTime? DeliveredCarrierAt { get; set; }
    
    [Column(TypeName = "timestamp without time zone")]
    public DateTime? DeliveredCustomerAt { get; set; }

    public List<OrderItem> Items { get; set; } = [];

    public List<OrderPayment> Payments { get; set; } = [];

    public List<Prediction> Predictions { get; set; } = [];
}