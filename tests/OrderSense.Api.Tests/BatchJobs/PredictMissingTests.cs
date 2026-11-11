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
    [Fact]
    public async Task PredictMissing_StaleQueuedJob_IsFailedAndNewRunStarts()
    {
        var (client, userId) = await UserAsync(Roles.Admin);
        var staleId = await factory.WithDbAsync(async db =>
        {
            var staleJob = new BatchJob
            {
                UserId = userId,
                Status = BatchJobStatus.Queued,
                FileName = PredictMissingProcessor.JobName,
                TotalRows = 1,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            };
            db.BatchJobs.Add(staleJob);
            await db.SaveChangesAsync();
            return staleJob.Id;
        });
        var response = await client.PostAsync(Url, null);
        var created = await response.Content.ReadFromJsonAsync<BatchJobDto>(ApiJson.Options);
        await WaitForFinishAsync(client, created!.Id);
        var staleDto = await client.GetFromJsonAsync<BatchJobDto>($"/api/batch-jobs/{staleId}", ApiJson.Options);
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(BatchJobStatus.Failed, staleDto!.Status);
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