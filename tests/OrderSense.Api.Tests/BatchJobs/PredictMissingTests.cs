using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Auth;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Predictions;
using OrderSense.Api.Tests.Infrastructure;
using OrderSense.Api.Tests.Orders;
namespace OrderSense.Api.Tests.BatchJobs;
[Collection(ApiCollection.Name)]
public class PredictMissingTests(ApiFactory factory)
{
    private const string Url = "/api/batch-jobs/predict-missing";
    [Fact]
    public async Task PredictMissing_AsAdmin_PredictsOrdersWithoutPrediction()
    {
        var orderIds = await SeedOrdersAsync(2);
        var (client, _) = await UserAsync(Roles.Admin);
        var response = await client.PostAsync(Url, null);
        var created = await response.Content.ReadFromJsonAsync<BatchJobDto>(ApiJson.Options);
        var job = await WaitForFinishAsync(client, created!.Id);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(BatchJobStatus.Done, job.Status);
        Assert.Equal(job.TotalRows, job.ProcessedRows);
        Assert.True(job.TotalRows >= 2);
        foreach (var id in orderIds)
        {
            var count = await factory.WithDbAsync(db => db.Predictions.CountAsync(p => p.OrderId == id));
            Assert.Equal(1, count);
        }
    }
    [Fact]
    public async Task PredictMissing_AsAnalyst_Returns403()
    {
        var (client, _) = await UserAsync(Roles.Analyst);
        var response = await client.PostAsync(Url, null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Fact]
    public async Task PredictMissing_WhenRunAlreadyInProgress_Returns409()
    {
        var (client, userId) = await UserAsync(Roles.Admin);
        var runningId = await factory.WithDbAsync(async db =>
        {
            var running = new BatchJob
            {
                UserId = userId,
                Kind = BatchJobKind.PredictMissing,
                Status = BatchJobStatus.Running,
                FileName = PredictMissingProcessor.JobName,
                TotalRows = 1,
            };
            db.BatchJobs.Add(running);
            await db.SaveChangesAsync();
            return running.Id;
        });
        try
        {
            var response = await client.PostAsync(Url, null);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
        finally
        {
            await factory.WithDbAsync(db => db.BatchJobs
                .Where(j => j.Id == runningId)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, BatchJobStatus.Done)));
        }
    }
    [Fact]
    public async Task PredictMissing_PredictionServiceDown_JobFails()
    {
        await SeedOrdersAsync(1);
        var (client, _) = await UserAsync(Roles.Admin);
        factory.Prediction.FailNextCall();
        try
        {
            var response = await client.PostAsync(Url, null);
            var created = await response.Content.ReadFromJsonAsync<BatchJobDto>(ApiJson.Options);
            var job = await WaitForFinishAsync(client, created!.Id);
            Assert.Equal(BatchJobStatus.Failed, job.Status);
            Assert.NotNull(job.Error);
        }
        finally
        {
            factory.Prediction.Reset();
        }
    }
    [Theory]
    [InlineData(BatchJobStatus.Queued)]
    [InlineData(BatchJobStatus.Running)]
    public async Task PredictMissing_StaleJob_IsFailedAndNewRunStarts(BatchJobStatus staleStatus)
    {
        await SeedOrdersAsync(1);
        var (client, userId) = await UserAsync(Roles.Admin);
        var staleId = await factory.WithDbAsync(async db =>
        {
            var staleJob = new BatchJob
            {
                UserId = userId,
                Kind = BatchJobKind.PredictMissing,
                Status = staleStatus,
                FileName = PredictMissingProcessor.JobName,
                TotalRows = 1,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            };
            db.BatchJobs.Add(staleJob);
            await db.SaveChangesAsync();
            return staleJob.Id;
        });
        var response = await client.PostAsync(Url, null);
        var created = await response.Content.ReadFromJsonAsync<BatchJobDto>(ApiJson.Options);
        await WaitForFinishAsync(client, created!.Id);
        var staleDto = await client.GetFromJsonAsync<BatchJobDto>($"/api/batch-jobs/{staleId}", ApiJson.Options);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(BatchJobStatus.Failed, staleDto!.Status);
    }
        [Fact]
    public async Task PredictMissing_ParallelRequests_NeverRunTwoAtOnce()
    {
        var orderIds = await SeedOrdersAsync(1);
        var (client, _) = await UserAsync(Roles.Admin);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsync(Url, null)));
        var allowed = new[] { HttpStatusCode.Accepted, HttpStatusCode.NoContent, HttpStatusCode.Conflict };
        Assert.All(responses, r => Assert.Contains(r.StatusCode, allowed));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Accepted);
        foreach (var started in responses.Where(r => r.StatusCode == HttpStatusCode.Accepted))
        {
            var created = await started.Content.ReadFromJsonAsync<BatchJobDto>(ApiJson.Options);
            await WaitForFinishAsync(client, created!.Id);
        }
        var predictions = await factory.WithDbAsync(db => db.Predictions.CountAsync(p => p.OrderId == orderIds[0]));
        Assert.Equal(1, predictions);
    }
    [Fact]
    public async Task PredictMissing_NothingToPredict_Returns204WithoutJob()
    {
        await SeedOrdersAsync(1);
        var (client, _) = await UserAsync(Roles.Admin);
        var first = await client.PostAsync(Url, null);
        var created = await first.Content.ReadFromJsonAsync<BatchJobDto>(ApiJson.Options);
        await WaitForFinishAsync(client, created!.Id);
        var jobsBefore = await factory.WithDbAsync(db => db.BatchJobs.CountAsync());
        var second = await client.PostAsync(Url, null);
        var jobsAfter = await factory.WithDbAsync(db => db.BatchJobs.CountAsync());
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(jobsBefore, jobsAfter);
    }
    [Fact]
    public async Task PredictMissing_UploadNamedLikeJob_DoesNotBlockRun()
    {
        await SeedOrdersAsync(1);
        var (client, userId) = await UserAsync(Roles.Admin);
        var uploadId = await SeedActiveJobAsync(userId, BatchJobKind.Upload);
        try
        {
            var response = await client.PostAsync(Url, null);
            var created = await response.Content.ReadFromJsonAsync<BatchJobDto>(ApiJson.Options);
            await WaitForFinishAsync(client, created!.Id);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }
        finally
        {
            await CloseJobAsync(uploadId);
        }
    }
    [Fact]
    public async Task Upload_NamedLikeJob_WhileRunActive_IsAccepted()
    {
        var (_, adminId) = await UserAsync(Roles.Admin);
        var (analyst, _) = await UserAsync(Roles.Analyst);
        var runningId = await SeedActiveJobAsync(adminId, BatchJobKind.PredictMissing);
        try
        {
            var file = new ByteArrayContent(BatchCsv.TemplateBytes());
            file.Headers.ContentType = new("text/csv");
            var response = await analyst.PostAsync("/api/batch-jobs", new MultipartFormDataContent { { file, "file", PredictMissingProcessor.JobName } });
            var created = await response.Content.ReadFromJsonAsync<BatchJobDto>(ApiJson.Options);
            await WaitForFinishAsync(analyst, created!.Id);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }
        finally
        {
            await CloseJobAsync(runningId);
        }
    }
    [Fact]
    public async Task PredictMissing_JobClosedWhileRunning_IsNotReopened()
    {
        var orderIds = await SeedOrdersAsync(1);
        var (client, _) = await UserAsync(Roles.Admin);
        factory.Prediction.BeforeBatch = () => factory.WithDbAsync(db => db.BatchJobs
            .Where(j => j.Kind == BatchJobKind.PredictMissing && j.Status == BatchJobStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, BatchJobStatus.Failed)
                .SetProperty(j => j.Error, "The job stopped responding")));
        try
        {
            var response = await client.PostAsync(Url, null);
            var created = await response.Content.ReadFromJsonAsync<BatchJobDto>(ApiJson.Options);
            await WaitForPredictionAsync(orderIds[0]);
            await Task.Delay(1000);
            var job = await client.GetFromJsonAsync<BatchJobDto>($"/api/batch-jobs/{created!.Id}", ApiJson.Options);
            Assert.Equal(BatchJobStatus.Failed, job!.Status);
            Assert.Equal("The job stopped responding", job.Error);
        }
        finally
        {
            factory.Prediction.Reset();
        }
    }
    private Task<Guid> SeedActiveJobAsync(Guid userId, BatchJobKind kind)
    {
        return factory.WithDbAsync(async db =>
        {
            var job = new BatchJob
            {
                UserId = userId,
                Kind = kind,
                Status = BatchJobStatus.Running,
                FileName = PredictMissingProcessor.JobName,
                TotalRows = 1,
            };
            db.BatchJobs.Add(job);
            await db.SaveChangesAsync();
            return job.Id;
        });
    }
    private Task<int> CloseJobAsync(Guid id)
    {
        return factory.WithDbAsync(db => db.BatchJobs
            .Where(j => j.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, BatchJobStatus.Done)));
    }
    private async Task WaitForPredictionAsync(string orderId)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (await factory.WithDbAsync(db => db.Predictions.AnyAsync(p => p.OrderId == orderId)))
            {
                return;
            }
            await Task.Delay(500);
        }
        throw new TimeoutException("Order was not predicted in 30 s");
    }
    private async Task<List<string>> SeedOrdersAsync(int count)
    {
        var data = OrderTestData.Create();
        var orders = Enumerable.Range(0, count).Select(_ => data.Order(new DateTime(2018, 3, 5, 10, 0, 0))).ToList();
        await factory.WithDbAsync(async db =>
        {
            db.Orders.AddRange(orders);
            await db.SaveChangesAsync();

            return 0;
        });
        return orders.Select(o => o.Id).ToList();
    }
    private async Task<(HttpClient Client, Guid UserId)> UserAsync(string role)
    {
        await factory.WithDbAsync(db => db.BatchJobs.Where(j => j.Kind == BatchJobKind.PredictMissing && (j.Status == BatchJobStatus.Queued || j.Status == BatchJobStatus.Running)).ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, BatchJobStatus.Failed)));
        var email = await factory.CreateUserAsync(role);
        var auth = await factory.LoginAsync(email);
        var userId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        return (factory.CreateClient(auth.AccessToken), userId);
    }
    private static async Task<BatchJobDto> WaitForFinishAsync(HttpClient client, Guid id)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var job = (await client.GetFromJsonAsync<BatchJobDto>($"/api/batch-jobs/{id}", ApiJson.Options))!;
            if (job.Status is BatchJobStatus.Done or BatchJobStatus.Failed)
            {
                return job;
            }
            await Task.Delay(500);
        }
        throw new TimeoutException("Predict-missing job did not finish in 30 s");
    }
}