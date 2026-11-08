using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using OrderSense.Api.Auth;
using OrderSense.Api.Demo;
using OrderSense.Api.Dtos;
using OrderSense.Api.Hubs;
using OrderSense.Api.Tests.Infrastructure;
using OrderSense.Api.Tests.Orders;

namespace OrderSense.Api.Tests.Demo;

[Collection(ApiCollection.Name)]
public class OrderSimulatorTests(ApiFactory factory)
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void Simulator_IsDisabledByDefault()
    {
        Assert.DoesNotContain(factory.Services.GetServices<IHostedService>(), s => s is OrderSimulator);
    }

    [Fact]
    public async Task Simulator_CreatesOrderEveryInterval_AndClientGetsIt()
    {
        factory.Prediction.Reset();
        await factory.SeedAsync(OrderTestData.Create().Order(new DateTime(2018, 3, 5)));
        var created = new List<OrderListItemDto>();
        var predicted = new TaskCompletionSource<string>();
        await using var connection = await ConnectAsync();
        connection.On<OrderListItemDto>(nameof(IOrdersClient.OrderCreated), o => created.Add(o));
        connection.On<string, PredictionDto>(nameof(IOrdersClient.PredictionUpdated), (id, _) => predicted.TrySetResult(id));
        using var simulator = await StartSimulatorAsync();

        _time.Advance(TimeSpan.FromSeconds(4));
        await Task.Delay(500);
        Assert.Empty(created);

        _time.Advance(TimeSpan.FromSeconds(1));
        var orderId = await predicted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var order = Assert.Single(created);
        Assert.Equal(order.Id, orderId);
        Assert.Equal("approved", order.Status);
        Assert.True(await factory.WithDbAsync(db => db.Predictions.AnyAsync(p => p.OrderId == orderId)));
    }

    [Fact]
    public async Task Simulator_KeepsWorking_WhenPredictionFails()
    {
        factory.Prediction.Reset();
        await factory.SeedAsync(OrderTestData.Create().Order(new DateTime(2018, 3, 5)));
        var created = new List<string>();
        var predicted = new TaskCompletionSource<string>();
        await using var connection = await ConnectAsync();
        connection.On<OrderListItemDto>(nameof(IOrdersClient.OrderCreated), o => created.Add(o.Id));
        connection.On<string, PredictionDto>(nameof(IOrdersClient.PredictionUpdated), (id, _) => predicted.TrySetResult(id));
        using var simulator = await StartSimulatorAsync();

        factory.Prediction.FailNextCall();
        _time.Advance(TimeSpan.FromSeconds(5));
        await Task.Delay(500);
        _time.Advance(TimeSpan.FromSeconds(5));
        var orderId = await predicted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(2, created.Count);
        Assert.Equal(created[1], orderId);
    }

    private async Task<OrderSimulator> StartSimulatorAsync()
    {
        var simulator = new OrderSimulator(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Services.GetRequiredService<IHubContext<OrdersHub, IOrdersClient>>(),
            new SimulatorOptions { Enabled = true, IntervalSeconds = 5 },
            _time,
            NullLogger<OrderSimulator>.Instance);
        await simulator.StartAsync(CancellationToken.None);

        return simulator;
    }

    private async Task<HubConnection> ConnectAsync()
    {
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(Roles.Analyst));
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/orders"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult<string?>(auth.AccessToken);
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions = ApiJson.Options)
            .Build();
        await connection.StartAsync();

        return connection;
    }
}