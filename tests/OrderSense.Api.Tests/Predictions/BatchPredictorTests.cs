using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Tests.Infrastructure;
using OrderSense.Api.Tests.Orders;
using OrderSense.Contracts;
using OrderSense.Api.Predictions;
namespace OrderSense.Api.Tests.Predictions;

public sealed class FakeBatchHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public int BatchCalls 
    { 
        get; 
        private set; 
    }
    public int SingleCalls 
    { 
        get; 
        private set; 
    }
    public bool RejectBatches 
    { 
        get; 
        set; 
    }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri?.AbsolutePath ?? "";
        if (path == "/predict/batch")
        {
            BatchCalls++;
            if (RejectBatches)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
            List<OrderFeatures>? batch = await request.Content!.ReadFromJsonAsync<List<OrderFeatures>>(Json, cancellationToken);
            int count = batch?.Count ?? 0;
            List<OrderPrediction> result = new List<OrderPrediction>();
            for (int i = 0; i < count; i++)
            {
                result.Add(CreatePrediction());
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result, options: Json) };
        }
        SingleCalls++;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(CreatePrediction(), options: Json) };
    }
    private static OrderPrediction CreatePrediction()
    {
        return new OrderPrediction { Probability = 0.9f, RiskLevel = "low", ModelVersion = "v-test" };
    }
}
public sealed class BatchPredictorTests
{
    private static HttpClient CreateClient(FakeBatchHandler handler)
    {
        HttpClient client = new HttpClient(handler);
        client.BaseAddress = new Uri("http://prediction.test");
        return client;
    }
    private static async Task SeedOrdersAsync(AppDbContext db, int withItems, int withoutItems)
    {
        OrderTestData data = OrderTestData.Create();
        DateTime day = new DateTime(2018, 3, 5, 10, 0, 0);
        for (int i = 0; i < withItems; i++)
        {
            db.Orders.Add(data.Order(day));
        }
        for (int i = 0; i < withoutItems; i++)
        {
            Order order = data.Order(day);
            order.Items = [];
            db.Orders.Add(order);
        }
        await db.SaveChangesAsync();
    }
    private static async Task SeedOldPredictionsAsync(AppDbContext db, string version)
    {
        ModelVersion model = new ModelVersion { Version = version, TrainedAt = DateTimeOffset.UtcNow, IsActive = false };
        db.ModelVersions.Add(model);
        await db.SaveChangesAsync();

        List<string> orderIds = await db.Orders.Select(o => o.Id).ToListAsync();

        foreach (string id in orderIds)
        {
            db.Predictions.Add(new Prediction
            {
                OrderId = id,
                ModelVersionId = model.Id,
                Probability = 0.5f,
                RiskLevel = RiskLevel.Medium,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            });
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Run_WithoutRecalculate_IgnoresOrdersWithOldVersionPredictions()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();
        await SeedOrdersAsync(db, 3, 0);
        await SeedOldPredictionsAsync(db, "v-old");
        using HttpClient client = CreateClient(new FakeBatchHandler());

        BatchRunResult result = await new BatchPredictor(db, client).RunAsync(1000, int.MaxValue, CancellationToken.None);

        Assert.Equal(0, result.Predicted);
        Assert.Equal(3, await db.Predictions.CountAsync());
    }

    [Fact]
    public async Task Run_Recalculate_AddsPredictionsOfNewVersionAndKeepsOldOnes()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();
        await SeedOrdersAsync(db, 3, 0);
        await SeedOldPredictionsAsync(db, "v-old");
        using HttpClient client = CreateClient(new FakeBatchHandler());

        BatchRunResult result = await new BatchPredictor(db, client) { RecalculateToVersion = "v-test" }
            .RunAsync(1000, int.MaxValue, CancellationToken.None);

        Assert.Equal(3, result.Predicted);
        Assert.Equal(6, await db.Predictions.CountAsync());
        Assert.Equal(3, await db.Orders.CountAsync(o => o.Predictions.Any(p => p.ModelVersion.Version == "v-test")));
    }

    [Fact]
    public async Task Run_Recalculate_SecondTime_DoesNothing()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();
        await SeedOrdersAsync(db, 3, 0);
        await SeedOldPredictionsAsync(db, "v-old");
        FakeBatchHandler handler = new FakeBatchHandler();
        using HttpClient client = CreateClient(handler);

        await new BatchPredictor(db, client) { RecalculateToVersion = "v-test" }.RunAsync(1000, int.MaxValue, CancellationToken.None);
        BatchRunResult second = await new BatchPredictor(db, client) { RecalculateToVersion = "v-test" }
            .RunAsync(1000, int.MaxValue, CancellationToken.None);

        Assert.Equal(0, second.Predicted);
        Assert.Equal(1, handler.BatchCalls);
    }

    [Fact]
    public async Task Run_Recalculate_WhenServiceReturnsOtherVersion_StillFinishes()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();
        await SeedOrdersAsync(db, 3, 0);
        using HttpClient client = CreateClient(new FakeBatchHandler());

        BatchRunResult result = await new BatchPredictor(db, client) { RecalculateToVersion = "v-other" }
            .RunAsync(1000, int.MaxValue, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(3, result.Predicted);
    }
    [Fact]
    public async Task Run_WritesPredictionForEveryOrderWithItems()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();
        await SeedOrdersAsync(db, 3, 1);
        FakeBatchHandler handler = new FakeBatchHandler();
        using HttpClient client = CreateClient(handler);
        BatchPredictor predictor = new BatchPredictor(db, client);
        BatchRunResult result = await predictor.RunAsync(1000, int.MaxValue, CancellationToken.None);
        Assert.Equal(3, result.Predicted);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(3, await db.Predictions.CountAsync());
        Assert.Equal(1, await db.ModelVersions.CountAsync());
    }
    [Fact]
    public async Task Run_SecondTime_DoesNotAddPredictions()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();
        await SeedOrdersAsync(db, 3, 0);
        FakeBatchHandler handler = new FakeBatchHandler();
        using HttpClient client = CreateClient(handler);
        await new BatchPredictor(db, client).RunAsync(1000, int.MaxValue, CancellationToken.None);
        BatchRunResult second = await new BatchPredictor(db, client).RunAsync(1000, int.MaxValue, CancellationToken.None);
        Assert.Equal(0, second.Predicted);
        Assert.Equal(1, handler.BatchCalls);
        Assert.Equal(3, await db.Predictions.CountAsync());
    }
    [Fact]
    public async Task Run_SplitsOrdersIntoBatches()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();
        await SeedOrdersAsync(db, 3, 0);
        FakeBatchHandler handler = new FakeBatchHandler();
        using HttpClient client = CreateClient(handler);
        BatchRunResult result = await new BatchPredictor(db, client).RunAsync(2, int.MaxValue, CancellationToken.None);
        Assert.Equal(3, result.Predicted);
        Assert.Equal(2, result.Batches);
        Assert.Equal(2, handler.BatchCalls);
    }
    [Fact]
    public async Task Run_RespectsMaxOrders()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();
        await SeedOrdersAsync(db, 3, 0);
        FakeBatchHandler handler = new FakeBatchHandler();
        using HttpClient client = CreateClient(handler);
        BatchRunResult result = await new BatchPredictor(db, client).RunAsync(1000, 2, CancellationToken.None);
        Assert.Equal(2, result.Predicted);
        Assert.Equal(1, await db.Orders.CountAsync(o => !o.Predictions.Any()));
    }
    [Fact]
    public async Task Run_WhenBatchIsRejected_FallsBackToSinglePredictions()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using AppDbContext db = database.CreateContext();
        await SeedOrdersAsync(db, 2, 0);
        FakeBatchHandler handler = new FakeBatchHandler { RejectBatches = true };
        using HttpClient client = CreateClient(handler);
        BatchRunResult result = await new BatchPredictor(db, client).RunAsync(1000, int.MaxValue, CancellationToken.None);
        Assert.Equal(2, result.Predicted);
        Assert.Equal(2, handler.SingleCalls);
        Assert.Equal(2, await db.Predictions.CountAsync());
    }
}