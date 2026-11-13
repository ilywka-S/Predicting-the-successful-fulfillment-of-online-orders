using Microsoft.AspNetCore.SignalR;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;

namespace OrderSense.Api.Hubs;

public class BatchProgressNotifier(IHubContext<OrdersHub, IOrdersClient> hub)
{
    public Task NotifyAsync(BatchJob job) => hub.Clients.User(job.UserId.ToString()).BatchJobProgress(BatchJobMapping.ToProgress(job));
    public Task NotifyAsync(Guid userId, BatchJobProgressDto progress) => hub.Clients.User(userId.ToString()).BatchJobProgress(progress);
}