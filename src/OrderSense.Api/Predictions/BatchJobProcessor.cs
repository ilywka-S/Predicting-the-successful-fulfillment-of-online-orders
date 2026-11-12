using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Hubs;
using OrderSense.Contracts;
namespace OrderSense.Api.Predictions;
public class BatchJobProcessor(AppDbContext db, PredictionClient client,
    BatchProgressNotifier notifier,
    JobStorage storage,
    TimeProvider timeProvider,
    ILogger<BatchJobProcessor> logger)
{
    public const int ChunkSize = 200;
    private const int ExplainParallelism = 8;
    public static readonly JsonSerializerOptions Json = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };
    private static readonly Action<ILogger, Guid, Exception?> JobFailed = LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(1, "BatchJobFailed"), "Batch job {JobId} failed");
    private static readonly Action<ILogger, Guid, Exception?> AlreadyRunning = LoggerMessage.Define<Guid>(LogLevel.Warning, new EventId(2, "BatchJobAlreadyRunning"), "Batch job {JobId} is already being processed, skipping the duplicate run");
    private static readonly Action<ILogger, Guid, Exception?> NotifyFailed = LoggerMessage.Define<Guid>(LogLevel.Warning, new EventId(3, "BatchJobNotifyFailed"), "Could not send progress notification for batch job {JobId}");
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(Guid jobId, CancellationToken ct)
    {
        using IStorageConnection connection = storage.GetConnection();
        IDisposable jobLock;
        try
        {
            jobLock = connection.AcquireDistributedLock($"batch-job:{jobId}", TimeSpan.Zero);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            AlreadyRunning(logger, jobId, e);

            return;
        }
        using (jobLock)
        {
            await ProcessAsync(jobId, ct);
        }
    }
    private async Task ProcessAsync(Guid jobId, CancellationToken ct)
    {
        BatchJob? job = await db.BatchJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null || job.Status is BatchJobStatus.Done or BatchJobStatus.Failed)
        {
            return;
        }
        int processed = 0;
        try
        {
            processed = await db.BatchResults.CountAsync(r => r.JobId == jobId && r.Probability != null, ct);
            await SetStatusAsync(job, BatchJobStatus.Running, processed, null, ct);
            int lastRow = 0;
            while (true)
            {
                List<BatchResult> rows = await db.BatchResults
                    .Where(r => r.JobId == jobId && r.Probability == null && r.RowNo > lastRow)
                    .OrderBy(r => r.RowNo)
                    .Take(ChunkSize)
                    .ToListAsync(ct);
                if (rows.Count == 0)
                {
                    break;
                }
                lastRow = rows[^1].RowNo;
                await ProcessChunkAsync(rows, ct);
                await db.SaveChangesAsync(ct);
                processed += rows.Count(r => r.Probability != null);
                db.ChangeTracker.Clear();
                await SetStatusAsync(job, BatchJobStatus.Running, processed, null, ct);
            }
            int failed = job.TotalRows - processed;
            if (processed == 0 && job.TotalRows > 0)
            {
                await SetStatusAsync(job, BatchJobStatus.Failed, processed, "No rows could be predicted", ct);
            }
            else
            {
                string? warning = failed > 0 ? $"Rows without prediction: {failed}" : null;
                await SetStatusAsync(job, BatchJobStatus.Done, processed, warning, ct);
            }
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            JobFailed(logger, jobId, e);
            string message = e is PredictionServiceUnavailableException
                ? "Prediction service is unavailable"
                : "Unexpected error while processing the file";
            await SetStatusAsync(job, BatchJobStatus.Failed, processed, message, CancellationToken.None);
        }
    }
    private async Task ProcessChunkAsync(List<BatchResult> rows, CancellationToken ct)
    {
        List<OrderFeatures> features = rows.Select(r => JsonSerializer.Deserialize<OrderFeatures>(r.Input, Json)!).ToList();
        IReadOnlyList<OrderPrediction?> predictions = await PredictAsync(features, ct);
        List<RiskFactor>[] factors = new List<RiskFactor>[rows.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, rows.Count), new ParallelOptions { MaxDegreeOfParallelism = ExplainParallelism, CancellationToken = ct },
            async (i, token) => factors[i] = predictions[i] is null ? [] : await ExplainAsync(features[i], token));
        for (int i = 0; i < rows.Count; i++)
        {
            OrderPrediction? prediction = predictions[i];
            if (prediction is null || !TryReadRisk(prediction, out RiskLevel risk))
            {
                continue;
            }
            rows[i].Probability = prediction.Probability;
            rows[i].RiskLevel = risk;
            rows[i].Factors = factors[i];
        }
    }
    private async Task<IReadOnlyList<OrderPrediction?>> PredictAsync(List<OrderFeatures> features, CancellationToken ct)
    {
        IReadOnlyList<OrderPrediction>? batch = await client.PredictBatchAsync(features, ct);
        if (batch is not null)
        {
            return batch.Select(p => (OrderPrediction?)p).ToList();
        }
        List<OrderPrediction?> result = [];
        foreach (OrderFeatures item in features)
        {
            try
            {
                result.Add(await client.PredictAsync(item, ct));
            }
            catch (PredictionServiceUnavailableException)
            {
                result.Add(null);
            }
        }
        return result;
    }
    private async Task<List<RiskFactor>> ExplainAsync(OrderFeatures features, CancellationToken ct)
    {
        List<RiskFactor> candidates = [];
        try
        {
            OrderExplanation explanation = await client.ExplainAsync(features, ct);
            foreach (FeatureContribution item in explanation.Factors)
            {
                if (float.IsFinite(item.Contribution))
                {
                    candidates.Add(new RiskFactor
                    {
                        Feature = item.Feature,
                        Contribution = item.Contribution,
                        Value = FactorValues.Numeric(features, item.Feature),
                    });
                }
            }
        }
        catch (PredictionServiceUnavailableException)
        {
            // рядок лишається без чинників
        }
        return RiskFactorMapping.Top(candidates, RiskFactorMapping.MaxFactors);
    }
    private static bool TryReadRisk(OrderPrediction p, out RiskLevel risk)
    {
        risk = RiskLevel.Low;
        if (!Enum.TryParse(p.RiskLevel, true, out RiskLevel parsed) || !Enum.IsDefined(parsed)
            || !float.IsFinite(p.Probability) || p.Probability is < 0 or > 1
            || string.IsNullOrWhiteSpace(p.ModelVersion))
        {
            return false;
        }
        risk = parsed;
        return true;
    }
    private async Task SetStatusAsync(BatchJob job, BatchJobStatus status, int processed, string? error, CancellationToken ct)
    {
        DateTimeOffset? finished = status is BatchJobStatus.Done or BatchJobStatus.Failed ? timeProvider.GetUtcNow() : null;
        await db.BatchJobs.Where(j => j.Id == job.Id).ExecuteUpdateAsync(s => s
            .SetProperty(j => j.Status, status)
            .SetProperty(j => j.ProcessedRows, processed)
            .SetProperty(j => j.Error, error)
            .SetProperty(j => j.FinishedAt, finished), ct);
        try
        {
            await notifier.NotifyAsync(job.UserId, new BatchJobProgressDto(job.Id, processed, job.TotalRows, status));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            NotifyFailed(logger, job.Id, e);
        }
    }
}