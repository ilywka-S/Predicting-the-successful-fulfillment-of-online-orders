using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Dtos;
using OrderSense.Api.Hubs;
using OrderSense.Api.Predictions;

namespace OrderSense.Api.Demo;

public sealed class OrderSimulator : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<OrdersHub, IOrdersClient> _hub;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrderSimulator> _logger;
    private readonly PeriodicTimer _timer;
    private DemoDataGenerator? _generator;

    public OrderSimulator(IServiceScopeFactory scopeFactory, IHubContext<OrdersHub, IOrdersClient> hub, SimulatorOptions options, TimeProvider timeProvider, ILogger<OrderSimulator> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _timeProvider = timeProvider;
        _logger = logger;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(options.IntervalSeconds), timeProvider);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CreateOrderAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Simulator could not create an order");
            }
        }
    }

    private async Task CreateOrderAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (_generator is null)
        {
            var sellers = await db.Sellers.AsNoTracking().ToListAsync(ct);
            var products = await db.Products.AsNoTracking().ToListAsync(ct);

            if (sellers.Count == 0 || products.Count == 0)
            {
                _logger.LogWarning("Simulator needs sellers and products in the database");
                return;
            }

            _generator = new DemoDataGenerator(sellers, products, Random.Shared.Next());
        }

        var order = _generator.CreateNewOrder(_timeProvider.GetUtcNow().DateTime);
        var zip = order.Customer.ZipPrefix;

        if (!await db.GeoZips.AnyAsync(z => z.ZipPrefix == zip, ct))
        {
            db.GeoZips.Add(_generator.GeoZips[zip]);
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        var total = order.Items.Sum(i => i.Price + i.FreightValue);
        await _hub.Clients.All.OrderCreated(new OrderListItemDto(order.Id, order.PurchasedAt, order.Status, order.Customer.State, order.Items.Count, total, null, null));

        await scope.ServiceProvider.GetRequiredService<OrderPredictor>().PredictAsync(order.Id, ct);
    }

    public override void Dispose()
    {
        _timer.Dispose();
        base.Dispose();
    }
}