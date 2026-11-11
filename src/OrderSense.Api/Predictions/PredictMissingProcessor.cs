using Hangfire;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Data;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Hubs;
using Polly;

namespace OrderSense.Api.Predictions;

public class PredictMissingProcessor(
    AppDbContext db,
    BatchPredictor predictor,
    BatchProgressNotifier notifier,
    TimeProvider timeProvider,
    ILogger<PredictMissingProcessor> logger)
{
    public const string JobName = "predict-missing";
    public const int BatchSize = 500;

    private static readonly Action<ILogger, Guid, Exception?> JobFailed = LoggerMessage.Define<Guid>(
        LogLevel.Error, new EventId(1, "PredictMissingFailed"), "Predict-missing job {JobId} failed");

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
        int lastProcessed = alreadyDone;
        await SetStatusAsync(job, BatchJobStatus.Running, lastProcessed, null, ct);

        try
        {
            BatchRunResult result = await predictor.RunAsync(
                BatchSize,
                int.MaxValue,
                ct,
                (progress, token) =>
                {
                    lastProcessed = Math.Min(job.TotalRows, alreadyDone + progress.Predicted);

                    return SetStatusAsync(job, BatchJobStatus.Running, lastProcessed, null, token);
                });

            lastProcessed = Math.Min(job.TotalRows, alreadyDone + result.Predicted);
            string? warning = result.Skipped > 0 ? $"Skipped orders: {result.Skipped}" : null;
            await SetStatusAsync(job, BatchJobStatus.Done, lastProcessed, warning, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            JobFailed(logger, jobId, e);
            string message = IsServiceFailure(e) ? "Prediction service is unavailable" : "Unexpected error while processing orders";
            await SetStatusAsync(job, BatchJobStatus.Failed, lastProcessed, message, CancellationToken.None);
        }
    }

    private static bool IsServiceFailure(Exception e)
    {
        return e is HttpRequestException or TaskCanceledException or ExecutionRejectedException or PredictionServiceUnavailableException;
    }

    private async Task SetStatusAsync(BatchJob job, BatchJobStatus status, int processed, string? error, CancellationToken ct)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset? finished = status is BatchJobStatus.Done or BatchJobStatus.Failed ? now : null;

        await db.BatchJobs.Where(j => j.Id == job.Id).ExecuteUpdateAsync(s => s
            .SetProperty(j => j.Status, status)
            .SetProperty(j => j.ProcessedRows, processed)
            .SetProperty(j => j.Error, error)
            .SetProperty(j => j.UpdatedAt, now)
            .SetProperty(j => j.FinishedAt, finished), ct);

        await notifier.NotifyAsync(job.UserId, new BatchJobProgressDto(job.Id, processed, job.TotalRows, status));
    }
}