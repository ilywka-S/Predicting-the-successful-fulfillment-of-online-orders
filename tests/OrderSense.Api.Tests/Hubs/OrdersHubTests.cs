using System.Net;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderSense.Api.Auth;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Hubs;
using OrderSense.Api.Tests.Infrastructure;
using OrderSense.Api.Tests.Orders;

namespace OrderSense.Api.Tests.Hubs;

[Collection(ApiCollection.Name)]
public class OrdersHubTests(ApiFactory factory)
{
    [Fact]
    public async Task Connect_WithoutToken_IsRejected()
    {
        await using var connection = CreateConnection(accessToken: null);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task Predict_NotifiesConnectedClients()
    {
        factory.Prediction.Reset();
        var order = OrderTestData.Create().Order(new DateTime(2018, 3, 5));
        await factory.SeedAsync(order);
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(Roles.Analyst));
        var received = new TaskCompletionSource<(string OrderId, PredictionDto Prediction)>();
        await using var connection = CreateConnection(auth.AccessToken);
        connection.On<string, PredictionDto>(nameof(IOrdersClient.PredictionUpdated), (id, p) => received.TrySetResult((id, p)));
        await connection.StartAsync();

        await factory.CreateClient(auth.AccessToken).PostAsync($"/api/orders/{order.Id}/predict", null);
        var (orderId, prediction) = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(order.Id, orderId);
        Assert.Equal(RiskLevel.Low, prediction.RiskLevel);
    }

    [Fact]
    public async Task BatchProgress_IsSentToJobOwner()
    {
        var email = await factory.CreateUserAsync(Roles.Analyst);
        var auth = await factory.LoginAsync(email);
        var userId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        var received = new TaskCompletionSource<BatchJobProgressDto>();
        await using var connection = CreateConnection(auth.AccessToken);
        connection.On<BatchJobProgressDto>(nameof(IOrdersClient.BatchJobProgress), p => received.TrySetResult(p));
        await connection.StartAsync();
        var job = new BatchJob { Id = Guid.NewGuid(), UserId = userId, FileName = "orders.csv", Status = BatchJobStatus.Running, TotalRows = 10, ProcessedRows = 4 };

        await factory.Services.GetRequiredService<BatchProgressNotifier>().NotifyAsync(job);
        var progress = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(new BatchJobProgressDto(job.Id, 4, 10, BatchJobStatus.Running), progress);
    }
    
    private HubConnection CreateConnection(string? accessToken) => new HubConnectionBuilder()
        .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/orders"), o =>
        {
            o.Transports = HttpTransportType.LongPolling;
            o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            o.AccessTokenProvider = () => Task.FromResult(accessToken);
        })
        .AddJsonProtocol(o => o.PayloadSerializerOptions = ApiJson.Options)
        .Build();
}