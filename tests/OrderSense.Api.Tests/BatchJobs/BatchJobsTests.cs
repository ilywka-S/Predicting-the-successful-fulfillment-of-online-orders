using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OrderSense.Api.Auth;
using OrderSense.Api.Data.Entities;
using OrderSense.Api.Dtos;
using OrderSense.Api.Tests.Infrastructure;

namespace OrderSense.Api.Tests.BatchJobs;

[Collection(ApiCollection.Name)]
public class BatchJobsTests(ApiFactory factory)
{
    [Fact]
    public async Task GetAll_ReturnsOnlyOwnJobs()
    {
        var (client, userId) = await AnalystAsync();
        var (_, otherId) = await AnalystAsync();
        var own = await SeedJobAsync(userId, BatchJobStatus.Done);
        var other = await SeedJobAsync(otherId, BatchJobStatus.Done);

        var jobs = await client.GetFromJsonAsync<List<BatchJobDto>>("/api/batch-jobs", ApiJson.Options);

        Assert.Equal([own.Id], jobs!.Select(j => j.Id));
        Assert.DoesNotContain(jobs!, j => j.Id == other.Id);
    }

    [Fact]
    public async Task GetAll_AsAdmin_ReturnsJobsOfAllUsers()
    {
        var (_, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId, BatchJobStatus.Queued);
        var auth = await factory.LoginAsync(await factory.CreateUserAsync(Roles.Admin));

        var jobs = await factory.CreateClient(auth.AccessToken).GetFromJsonAsync<List<BatchJobDto>>("/api/batch-jobs", ApiJson.Options);

        Assert.Contains(jobs!, j => j.Id == job.Id);
    }

    [Fact]
    public async Task GetById_ReturnsStatusAndProgress()
    {
        var (client, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId, BatchJobStatus.Running);

        var dto = await client.GetFromJsonAsync<BatchJobDto>($"/api/batch-jobs/{job.Id}", ApiJson.Options);

        Assert.Equal(BatchJobStatus.Running, dto!.Status);
        Assert.Equal(3, dto.TotalRows);
        Assert.Equal(job.ProcessedRows, dto.ProcessedRows);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/results")]
    [InlineData("/export")]
    public async Task OtherUsersJob_Returns404(string suffix)
    {
        var (_, otherId) = await AnalystAsync();
        var job = await SeedJobAsync(otherId, BatchJobStatus.Done);
        var (client, _) = await AnalystAsync();

        var response = await client.GetAsync($"/api/batch-jobs/{job.Id}{suffix}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetResults_ReturnsPageOrderedByRowWithTopFiveFactors()
    {
        var (client, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId, BatchJobStatus.Done);

        var page = await client.GetFromJsonAsync<PagedResponse<BatchResultDto>>($"/api/batch-jobs/{job.Id}/results?page=1&pageSize=2", ApiJson.Options);

        Assert.Equal(3, page!.Total);
        Assert.Equal([1, 2], page.Items.Select(r => r.RowNo));
        Assert.Equal(5, page.Items[0].Factors.Count);
        Assert.Equal("DistanceKm", page.Items[0].Factors[0].Feature);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    public async Task GetResults_InvalidPaging_Returns400(string query)
    {
        var (client, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId, BatchJobStatus.Done);

        var response = await client.GetAsync($"/api/batch-jobs/{job.Id}/results?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Export_DoneJob_ReturnsUtf8CsvForExcel()
    {
        var (client, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId, BatchJobStatus.Done);

        var response = await client.GetAsync($"/api/batch-jobs/{job.Id}/export");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("orders-predictions.csv", response.Content.Headers.ContentDisposition!.FileName);
        Assert.Equal(Encoding.UTF8.Preamble.ToArray(), bytes[..3]);
        Assert.Equal(4, lines.Length);
        Assert.Equal("row_no;ItemsCount;CustomerState;probability;risk_level;factors", lines[0]);
        Assert.StartsWith("1;2;SP;0.85;low;Відстань продавець -> клієнт, Обіцяний термін доставки", lines[1]);
        Assert.Equal("3;1;\"R;J\";;;", lines[3]);
    }

    [Fact]
    public async Task Export_RunningJob_Returns409()
    {
        var (client, userId) = await AnalystAsync();
        var job = await SeedJobAsync(userId, BatchJobStatus.Running);

        var response = await client.GetAsync($"/api/batch-jobs/{job.Id}/export");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<(HttpClient Client, Guid UserId)> AnalystAsync()
    {
        var email = await factory.CreateUserAsync(Roles.Analyst);
        var auth = await factory.LoginAsync(email);
        var userId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

        return (factory.CreateClient(auth.AccessToken), userId);
    }

    private Task<BatchJob> SeedJobAsync(Guid userId, BatchJobStatus status) => factory.WithDbAsync(async db =>
    {
        var job = new BatchJob
        {
            UserId = userId,
            Status = status,
            FileName = "orders.csv",
            TotalRows = 3,
            ProcessedRows = status == BatchJobStatus.Running ? 1 : 3,
            Results =
            [
                Result(2, """{"ItemsCount": 1, "CustomerState": "RJ"}""", 0.4f, RiskLevel.High),
                Result(1, """{"ItemsCount": 2, "CustomerState": "SP"}""", 0.85f, RiskLevel.Low),
                new BatchResult { RowNo = 3, Input = """{"ItemsCount": 1, "CustomerState": "R;J"}""" },
            ],
        };

        db.BatchJobs.Add(job);
        await db.SaveChangesAsync();

        return job;
    });

    private static BatchResult Result(int rowNo, string input, float probability, RiskLevel risk) => new()
    {
        RowNo = rowNo,
        Input = input,
        Probability = probability,
        RiskLevel = risk,
        Factors =
        [
            new RiskFactor { Feature = "ItemsCount", Contribution = 0.1f },
            new RiskFactor { Feature = "TotalPrice", Contribution = -0.2f },
            new RiskFactor { Feature = "DistanceKm", Contribution = -0.9f },
            new RiskFactor { Feature = "PromisedDays", Contribution = 0.5f },
            new RiskFactor { Feature = "SellerState", Contribution = 0.3f },
            new RiskFactor { Feature = "PaymentType", Contribution = 0.05f },
        ],
    };
}