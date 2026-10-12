using OrderSense.Api.Data.Entities;

namespace OrderSense.Api.Tests.Orders;

public sealed class OrderTestData(string prefix)
{
    private readonly ModelVersion _model = new() { Version = $"test-{prefix}", TrainedAt = DateTimeOffset.UtcNow };
    private int _counter;

    public static OrderTestData Create() => new(Guid.NewGuid().ToString("N")[..8]);

    public string Prefix => prefix;
    private string NextId() => $"{prefix}{++_counter:D24}";

    public Order Order(DateTime purchasedAt, string status = "delivered", string state = "SP")
    {
        var orderId = NextId();
        var customer = new Customer { Id = NextId(), UniqueId = NextId(), ZipPrefix = "01001", City = "sao paulo", State = state };
        var seller = new Seller { Id = NextId(), ZipPrefix = "01001", City = "sao paulo", State = "SP" };
        var product = new Product { Id = NextId(), Category = "beleza_saude", CategoryEn = "health_beauty", WeightG = 500 };

        return new Order
        {
            Id = orderId,
            CustomerId = customer.Id,
            Customer = customer,
            Status = status,
            PurchasedAt = purchasedAt,
            EstimatedDeliveryAt = purchasedAt.Date.AddDays(10),
            Items =
            [
                new OrderItem
                {
                    OrderId = orderId, ItemNo = 1, ProductId = product.Id, Product = product,
                    SellerId = seller.Id, Seller = seller, Price = 100m, FreightValue = 15m,
                },
            ],
            Payments = [new OrderPayment { OrderId = orderId, Sequential = 1, Type = "credit_card", Installments = 1, Value = 115m }],
        };
    }

    public void AddPrediction(Order order, RiskLevel risk, DateTimeOffset createdAt, params RiskFactor[] factors) =>
        order.Predictions.Add(new Prediction
        {
            OrderId = order.Id,
            ModelVersion = _model,
            Probability = risk switch { RiskLevel.Low => 0.9f, RiskLevel.Medium => 0.7f, _ => 0.4f },
            RiskLevel = risk,
            CreatedAt = createdAt,
            Factors = [.. factors],
        });
}