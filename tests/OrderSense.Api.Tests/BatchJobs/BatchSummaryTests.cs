using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Auth;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.BatchJobs;

[Collection(ApiCollection.Name)]
public class BatchSummaryTests(ApiFactory factory)
{
    [Fact]
    public async Task Summary_CountsRiskLevelsAndAverageProbability()
    {
        var (client, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId);

        var summary = await client.GetFromJsonAsync<BatchJobSummaryDto>($"/api/batch-jobs/{job.Id}/summary", ApiJson.Options);

        Assert.Equal(job.Id, summary!.JobId);
        Assert.Equal("orders.csv", summary.FileName);
        Assert.Equal(4, summary.TotalRows);
        Assert.Equal(3, summary.PredictedRows);
        Assert.Equal(1, summary.Low);
        Assert.Equal(1, summary.Medium);
        Assert.Equal(1, summary.High);
        Assert.Equal(0.11, summary.AverageProbability!.Value, 3);
    }

    [Fact]
    public async Task Summary_GroupsRowsByCategoryStateAndPayment()
    {
        var (client, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId);

        var summary = await client.GetFromJsonAsync<BatchJobSummaryDto>($"/api/batch-jobs/{job.Id}/summary", ApiJson.Options);

        Assert.Equal(["RJ", "SP"], summary!.ByCustomerState.Select(p => p.Key));
        var rj = summary.ByCustomerState[0];
        Assert.Equal(2, rj.Orders);
        Assert.Equal(1, rj.Predicted);
        Assert.Equal(1.0, rj.RiskyShare);
        Assert.Equal(0.5, summary.ByCustomerState[1].RiskyShare);
        Assert.Equal(3, summary.ByCategory.Count);
        Assert.Equal(2, summary.ByCategory[0].Orders);
        Assert.Equal("", summary.ByCategory[1].Key);
        Assert.Equal(["credit_card", "", "boleto"], summary.ByPaymentType.Select(p => p.Key));
    }

    [Fact]
    public async Task Summary_TranslatesCategoriesLikeProductCatalog()
    {
        var (client, userId) = await AnalystAsync();
        await factory.WithDbAsync(async db =>
        {
            db.Products.Add(new Product { Id = Guid.NewGuid().ToString("N"), Category = "informatica_acessorios", CategoryEn = "computers_accessories" });
            return await db.SaveChangesAsync();
        });
        var job = await SeedJobAsync(userId);

        var summary = await client.GetFromJsonAsync<BatchJobSummaryDto>($"/api/batch-jobs/{job.Id}/summary", ApiJson.Options);

        Assert.Contains(summary!.ByCategory, p => p.Key == "computers_accessories");
        Assert.DoesNotContain(summary.ByCategory, p => p.Key == "informatica_acessorios");
    }

    [Fact]
    public async Task Summary_CountsStrongestRiskFactorOfRiskyRows()
    {
        var (client, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId);

        var summary = await client.GetFromJsonAsync<BatchJobSummaryDto>($"/api/batch-jobs/{job.Id}/summary", ApiJson.Options);

        Assert.Equal(["DistanceKm", "PromisedDays"], summary!.TopFactors.Select(f => f.Feature));
        Assert.All(summary.TopFactors, f => Assert.Equal(1, f.Rows));
        Assert.Equal("Обіцяний термін доставки", summary.TopFactors[1].Label);
    }

    [Fact]
    public async Task Summary_WithTop_LimitsGroups()
    {
        var (client, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId);

        var summary = await client.GetFromJsonAsync<BatchJobSummaryDto>($"/api/batch-jobs/{job.Id}/summary?top=1", ApiJson.Options);

        Assert.Single(summary!.ByCategory);
        Assert.Equal(2, summary.ByCategory[0].Orders);
        Assert.Single(summary.ByCustomerState);
        Assert.Single(summary.ByPaymentType);
    }

    [Fact]
    public async Task Summary_WithoutPredictions_HasNoAverageOrShare()
    {
        var (client, userId) = await AnalystAsync();
        var job = await factory.WithDbAsync(async db =>
        {
            var created = new BatchJob
            {
                UserId = userId,
                Status = BatchJobStatus.Failed,
                FileName = "failed.csv",
                TotalRows = 1,
                Results = [new BatchResult { RowNo = 1, Input = """{"CustomerState": "SP"}""" }],
            };
            db.BatchJobs.Add(created);
            await db.SaveChangesAsync();
            return created;
        });

        var summary = await client.GetFromJsonAsync<BatchJobSummaryDto>($"/api/batch-jobs/{job.Id}/summary", ApiJson.Options);

        Assert.Equal(0, summary!.PredictedRows);
        Assert.Null(summary.AverageProbability);
        Assert.Null(summary.ByCustomerState.Single().RiskyShare);
        Assert.Empty(summary.TopFactors);
    }

    [Fact]
    public async Task Summary_OfOtherUsersJob_Returns404()
    {
        var (client, _) = await AnalystAsync();
        var (_, otherId) = await AnalystAsync();
        var job = await SeedJobAsync(otherId);

        var response = await client.GetAsync($"/api/batch-jobs/{job.Id}/summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Summary_AsAdmin_OpensAnyJob()
    {
        var (_, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId);
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(Roles.Admin));

        var response = await factory.CreateClient(auth.AccessToken).GetAsync($"/api/batch-jobs/{job.Id}/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(BatchJobStatus.Queued)]
    [InlineData(BatchJobStatus.Running)]
    public async Task Summary_OfUnfinishedJob_Returns409(BatchJobStatus status)
    {
        var (client, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId);
        await factory.WithDbAsync(db => db.BatchJobs
            .Where(j => j.Id == job.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, status)));

        var response = await client.GetAsync($"/api/batch-jobs/{job.Id}/summary");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task Summary_WithInvalidTop_Returns400(int top)
    {
        var (client, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId);

        var response = await client.GetAsync($"/api/batch-jobs/{job.Id}/summary?top={top}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<BatchJob> SeedJobAsync(Guid userId) => factory.WithDbAsync(async db =>
    {
        var job = new BatchJob
        {
            UserId = userId,
            Status = BatchJobStatus.Done,
            FileName = "orders.csv",
            TotalRows = 4,
            ProcessedRows = 3,
            Results =
            [
                Row(1, "SP", "cama_mesa_banho", "credit_card", 0.03f, RiskLevel.Low, new RiskFactor { Feature = "PromisedDays", Contribution = -0.01f }),
                Row(2, "SP", "cama_mesa_banho", "boleto", 0.2f, RiskLevel.High,
                    new RiskFactor { Feature = "DistanceKm", Contribution = 0.02f },
                    new RiskFactor { Feature = "PromisedDays", Contribution = 0.05f }),
                Row(3, "RJ", "informatica_acessorios", "credit_card", 0.1f, RiskLevel.Medium,
                    new RiskFactor { Feature = "PromisedDays", Contribution = 0.01f },
                    new RiskFactor { Feature = "DistanceKm", Contribution = 0.03f }),
                new BatchResult { RowNo = 4, Input = """{"CustomerState": "RJ", "MainCategory": "", "PaymentType": ""}""" },
            ],
        };

        db.BatchJobs.Add(job);
        await db.SaveChangesAsync();

        return job;
    });

    private static BatchResult Row(int rowNo, string state, string category, string payment, float probability, RiskLevel risk, params RiskFactor[] factors) => new()
    {
        RowNo = rowNo,
        Input = $$"""{"CustomerState": "{{state}}", "MainCategory": "{{category}}", "PaymentType": "{{payment}}"}""",
        Probability = probability,
        RiskLevel = risk,
        Factors = [.. factors],
    };

    private async Task<(HttpClient Client, Guid UserId)> AnalystAsync()
    {
        var email = await factory.CreateUserAsync(Roles.Analyst);
        var auth = await factory.LoginAsync(email);
        var userId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

        return (factory.CreateClient(auth.AccessToken), userId);
    }
}
