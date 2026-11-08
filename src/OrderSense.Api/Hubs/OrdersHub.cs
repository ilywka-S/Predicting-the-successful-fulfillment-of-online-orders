using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrderSense.Api.Dtos;

namespace OrderSense.Api.Hubs;

[Authorize]
public class OrdersHub : Hub<IOrdersClient>;

public interface IOrdersClient
{
    Task OrderCreated(OrderListItemDto order);

    Task PredictionUpdated(string orderId, PredictionDto prediction);

    Task BatchJobProgress(BatchJobDto job);
    
    Task BatchJobProgress(BatchJobProgressDto progress);
}