using Hangfire;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Hubs;
namespace OrderSense.Api.Predictions;
public class PredictMissingProcessor(AppDbContext db, BatchPredictor predictor,
    BatchProgressNotifier notifier,
    TimeProvider timeProvider,
    ILogger<PredictMissingProcessor> logger)
{
    public const string JobName = "predict-missing";
    public const int BatchSize = 500;
    private static readonly Action<ILogger, Guid, Exception?> JobFailed = LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(1, "PredictMissingFailed"), "Predict-missing job {JobId} failed");
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(Guid jobId, CancellationToken ct)
    {
        BatchJob? job = await db.BatchJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null || job.Status is BatchJobStatus.Done or BatchJobStatus.Failed)
        {
            return;
        }
        int remaining = await db.Orders.CountAsync(o => o.Items.Any() && !o.Predictions.Any(), ct);
        int alreadyDone = Math.Max(0, job.TotalRows - remaining);
        await SetStatusAsync(job, BatchJobStatus.Running, alreadyDone, null, ct);
        try
        {
            BatchRunResult result = await predictor.RunAsync(
                BatchSize,
                int.MaxValue,
                ct,
                (progress, token) => SetStatusAsync(job, BatchJobStatus.Running, Math.Min(job.TotalRows, alreadyDone + progress.Predicted + progress.Skipped), null, token));

            string? warning = result.Skipped > 0 ? $"{result.Skipped} orders were skipped: the prediction service rejected them" : null;
            await SetStatusAsync(job, BatchJobStatus.Done, job.TotalRows, warning, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            JobFailed(logger, jobId, e);
            await SetStatusAsync(job, BatchJobStatus.Failed, alreadyDone, "Prediction service is unavailable", CancellationToken.None);
        }
    }
    private async Task SetStatusAsync(BatchJob job, BatchJobStatus status, int processed, string? error, CancellationToken ct)
    {
        DateTimeOffset? finished = status is BatchJobStatus.Done or BatchJobStatus.Failed ? timeProvider.GetUtcNow() : null;
        await db.BatchJobs.Where(j => j.Id == job.Id).ExecuteUpdateAsync(s => s
            .SetProperty(j => j.Status, status)
            .SetProperty(j => j.ProcessedRows, processed)
            .SetProperty(j => j.Error, error)
            .SetProperty(j => j.FinishedAt, finished), ct);
        await notifier.NotifyAsync(job.UserId, new BatchJobProgressDto(job.Id, processed, job.TotalRows, status));
    }
}